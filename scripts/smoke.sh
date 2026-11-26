#!/usr/bin/env bash
# T23 smoke: boot the API on its own scratch DB (rmg_t23_scratch), curl every
# pilot endpoint (login, facilities, users, roles, materials, uoms, styles,
# approve, boms, lines, release, buyers, orders, confirm) plus 3 negative
# probes expecting 4xx, then tear everything down. Non-zero exit on any probe
# mismatch.
#
# Scratch isolation: the API process gets a temp-dir .env (its config walk-up
# finds it first — env-var prefixes do NOT beat a .env), so this works from any
# checkout regardless of what the repo .env targets. The dev DB rmg_teamzlab is
# never touched. API on :5101 (SMOKE_API_URL overrides). Do not run concurrently
# with the E2E harness — both use rmg_t23_scratch.
set -u
cd "$(dirname "$0")/.." || exit 1
REPO="$(pwd)"
API_URL="${SMOKE_API_URL:-http://localhost:5101}"
API_PORT="$(printf '%s' "$API_URL" | sed -E 's|.*:([0-9]+).*|\1|')"

PSQL="psql"
for v in 18 17 16; do
  [ -x "/c/Program Files/PostgreSQL/$v/bin/psql.exe" ] && PSQL="/c/Program Files/PostgreSQL/$v/bin/psql.exe" && break
done

envval() { sed -nE "s/^$1=([^;]*)\$/\1/p" "$REPO/.env" 2>/dev/null | head -1; }

DBHOST=$(envval 'ConnectionStrings__Default.*Host' ); DBHOST=${DBHOST:-localhost}
DBPORT=$(envval 'ConnectionStrings__Default.*Port'); DBPORT=${DBPORT:-5432}
DBUSER=$(envval 'ConnectionStrings__Default.*Username'); DBUSER=${DBUSER:-postgres}
DBPASS=$(envval 'ConnectionStrings__Default.*Password')
JWTKEY=$(envval 'Jwt__Key'); JWTKEY=${JWTKEY:-dev-only-secret-key-change-me-32chars-min!}
SEEDPW=$(envval 'Seed__AdminPassword'); SEEDPW=${SEEDPW:-Admin#12345}
DB="rmg_t23_scratch"
CS="Host=$DBHOST;Port=$DBPORT;Database=$DB;Username=$DBUSER;Password=$DBPASS"

PG() { PGPASSWORD="$DBPASS" "$PSQL" -h "$DBHOST" -p "$DBPORT" -U "$DBUSER" -d "$1" -v ON_ERROR_STOP=1 -c "$2"; }

TMPENV=""
API_PID=""
cleanup() {
  [ -n "$API_PID" ] && taskkill //PID "$API_PID" //T //F >/dev/null 2>&1
  PG postgres "DROP DATABASE IF EXISTS $DB WITH (FORCE)" >/dev/null 2>&1
  # The API child holds cwd inside TMPENV; the lock releases a beat after the kill.
  [ -n "$TMPENV" ] && { rm -rf "$TMPENV" 2>/dev/null || { sleep 2; rm -rf "$TMPENV" 2>/dev/null || true; }; }
}
trap cleanup EXIT

PASS=0; FAIL=0
probe() { # probe NAME WANT_STATUS METHOD API_V1_PATH [BODY] [TOKEN]
  local name="$1" want="$2" method="$3" path="$4" body="${5:-}" token="${6:-}"
  local args=(-o "$TMPENV/out" -w '%{http_code}' -X "$method" "$API_URL/api/v1$path")
  [ -n "$token" ] && args+=(-H "Authorization: Bearer $token")
  [ -n "$body" ] && args+=(-H "Content-Type: application/json" -d "$body")
  local got; got=$(curl -s "${args[@]}")
  if [ "$got" = "$want" ]; then
    echo "PASS  $name"; PASS=$((PASS+1)); return 0
  fi
  echo "FAIL  $name  ($method $path -> $got, want $want)"; sed -n '1,3p' "$TMPENV/out"; FAIL=$((FAIL+1)); return 1
}

first_id() { grep -oE '"id":"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}"' | head -1 | cut -d'"' -f4; }
version_id() { grep -oE '"versions":\[\{"id":"[0-9a-f-]{36}"' | head -1 | grep -oE '[0-9a-f-]{36}'; }

TMPENV="$(mktemp -d)" || exit 1

echo "== boot: scratch DB $DB + API on $API_URL =="
curl -s -o /dev/null --max-time 2 "$API_URL/" && { echo "FAIL: $API_URL already in use — free it or set SMOKE_API_URL"; exit 1; }

# The API's .env walk-up starts at cwd, so a temp dir holding a scratch .env
# isolates it from whatever the checkout's own .env targets.
printf 'ConnectionStrings__Default=%s\nJwt__Key=%s\nSeed__AdminPassword=%s\n' "$CS" "$JWTKEY" "$SEEDPW" > "$TMPENV/.env"

PG postgres "DROP DATABASE IF EXISTS $DB WITH (FORCE)" >/dev/null || exit 1
ConnectionStrings__Default="$CS" dotnet ef database update --project src/Database --context RmgDbContext >/dev/null || { echo "FAIL: migrate"; exit 1; }

cd "$TMPENV"
ASPNETCORE_ENVIRONMENT=Development dotnet run --project "$REPO/src/Api" --no-launch-profile --urls "$API_URL" >/dev/null 2>&1 &
API_PID=$!
cd "$REPO"

for i in $(seq 1 120); do curl -s -o /dev/null --max-time 2 "$API_URL/" && break; sleep 1; [ "$i" = 120 ] && { echo "FAIL: API never became ready"; exit 1; }; done
echo "== API ready =="

# Resolve the API's Windows PID from the listening port (MSYS $! is not a
# Windows PID, so taskkill //T would miss the tree).
API_PID=$(netstat -ano | grep ":$API_PORT" | grep LISTENING | head -1 | awk '{print $NF}')

echo "== probes =="
probe "login"               200 POST /auth/login '{"email":"admin@tzl.demo","password":"'"$SEEDPW"'"}'
TOKEN=$(sed -nE 's/.*"accessToken":"([^"]+)".*/\1/p' "$TMPENV/out")
[ -n "$TOKEN" ] || { echo "FAIL: no token from login"; exit 1; }

probe "facilities"          200 GET /org/facilities "" "$TOKEN"
probe "users"               200 GET /iam/users "" "$TOKEN"
probe "roles"               200 GET /iam/roles "" "$TOKEN"
probe "uoms"                200 GET /pdm/uoms "" "$TOKEN"
probe "materials list"      200 GET /pdm/materials "" "$TOKEN"

probe "create material"     201 POST /pdm/materials '{"code":"SMOKE-RM-01","name":"Smoke fabric","category":"Fabric","baseUomCode":"KG"}' "$TOKEN"
MATERIAL_ID=$(first_id < "$TMPENV/out")

probe "create buyer"        201 POST /ord/buyers '{"code":"SMOKE-BUY-01","name":"Smoke Buyer Ltd","country":"BD","currency":"USD"}' "$TOKEN"
BUYER_ID=$(first_id < "$TMPENV/out")

probe "create style"        201 POST /pdm/styles '{"code":"SMOKE-STY-01","name":"Smoke Shirt","buyerId":"'"$BUYER_ID"'","variants":[{"colorCode":"NAVY","sizeCode":"M"}]}' "$TOKEN"
STYLE_ID=$(first_id < "$TMPENV/out"); VERSION_ID=$(version_id < "$TMPENV/out")

probe "approve style"       200 POST "/pdm/styles/$STYLE_ID/approve" "" "$TOKEN"
probe "create bom"          201 POST /pdm/boms '{"styleVersionId":"'"$VERSION_ID"'"}' "$TOKEN"
BOM_ID=$(first_id < "$TMPENV/out")
probe "add bom line"        201 POST "/pdm/boms/$BOM_ID/lines" '{"materialId":"'"$MATERIAL_ID"'","qtyPerUnit":1.2,"uomCode":"KG","wastePct":5}' "$TOKEN"
probe "release bom"         200 POST "/pdm/boms/$BOM_ID/approve" "" "$TOKEN"

probe "create order"        201 POST /ord/customer-orders '{"code":"SMOKE-ORD-01","buyerId":"'"$BUYER_ID"'","buyerPo":"SMOKE-PO-01","currency":"USD","orderDate":"2026-12-15","lines":[{"styleVersionId":"'"$VERSION_ID"'","price":4.5,"sizes":[{"colorCode":"NAVY","sizeCode":"M","qty":50}]}]}' "$TOKEN"
ORDER_ID=$(first_id < "$TMPENV/out")
probe "confirm order"       200 POST "/ord/customer-orders/$ORDER_ID/confirm" '{"expectedRevision":1}' "$TOKEN"

echo "== negative probes (expect 4xx) =="
probe "login wrong password"   401 POST /auth/login '{"email":"admin@tzl.demo","password":"nope"}'
probe "material with bad uom"  422 POST /pdm/materials '{"code":"SMOKE-RM-BAD","name":"Bad uom","category":"Fabric","baseUomCode":"BAD"}' "$TOKEN"
probe "unknown order id"       404 GET "/ord/customer-orders/00000000-0000-0000-0000-000000000000" "" "$TOKEN"

echo "== summary: $PASS passed, $FAIL failed =="
[ "$FAIL" -eq 0 ]
