# RMG Operations Platform — Foundation + Pilot Slice: Design Specification

**Date:** 2026-11-25 · **Status:** approved design, awaiting spec review
**Normative source:** `International_RMG_Platform_Coding_Specification_v1.docx` (v1.0, 2026-11-23)
**Scope ruling (owner):** this drive = Sprint 0 (foundation) + Sprint 1 (pilot slice) per FND-029. Later sprints are separate drives.
**Design approach ruling (owner):** Approach A — spec-faithful minimal (FND-006 structure, API-issued JWT, no external IdP/container in pilot).

---

## 1. Purpose

Build the first production-ready increment of a multilingual garment-manufacturing ERP:
platform foundation (tenancy, identity, envelope, outbox, audit, migrations, seed) plus one
end-to-end merchandising flow — **buyer → style (+variants) → BOM (approve) → customer order
→ confirm** — with tests, UI, and the reference design look.

Success criteria:
1. Solution and web app build clean on the pinned toolchain.
2. Full test suite green (unit + integration + contract + security) on the pinned PostgreSQL.
3. Seeded demo tenant: login → create buyer → style with color/size variants → BOM lines →
   order capture → confirm, browser-verifiable end to end (Playwright).
4. Negative paths proven: cross-tenant substitution, stale revision, duplicate idempotency
   key, missing permission, invalid state transition, bad UOM/qty.
5. Key screens match the TailwindAdmin reference tokens, light AND dark, en AND bn.

## 2. Stack & toolchain (FND-002 gate)

| Layer | Pinned choice | Note |
|---|---|---|
| API runtime | .NET 11 (`net11.0`), ASP.NET Core 11 | SDK 11.0.100 verified present |
| ORM | EF Core 10.x + Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 | provider major stays aligned with EF major; upgrade both to 11 only when provider 11 is stable GA |
| Database | PostgreSQL @ localhost:5433, db `rmg_teamzlab` | probe & record server `server_version` at build start; credentials in gitignored `.env` only — never in committed files |
| Frontend | Next.js 16 (App Router, TypeScript), Node ≥ 20 (24.16.0 present) | lock exact patch at scaffold time |
| Tests | xUnit + Testcontainers-free integration harness; Playwright (MCP) for E2E | §10 |
| API docs | Built-in ASP.NET Core OpenAPI generation → `contracts/openapi/` | description, not retry semantics (FND-011) |

Compatibility gate: a clean `net11.0` restore/build plus the PostgreSQL integration suite is
the acceptance test of the mixed-major pairing. If it cannot be made green, fall back to the
fully aligned .NET 10 LTS baseline (FND-002 fallback clause) and record an ADR.

## 3. Architecture

Modular monolith (FND-004). API owns domain invariants, authorization, and all writes;
PostgreSQL is the transactional source of truth; Next.js is a stateless client.

Repository layout (FND-006, trimmed to pilot modules — later modules join the same pattern):

```
apps/web                     Next.js 16 client
src/Api                      host, middleware, endpoint registration, OpenAPI
src/Modules/Organization     {Domain, Application, Infrastructure, Contracts}
src/Modules/Identity         〃
src/Modules/ProductEngineering  〃 (PDM)
src/Modules/Merchandising    〃 (ORD)
src/BuildingBlocks           Tenancy, Auth, Common (envelope/pagination/errors), Outbox, Audit
src/Workers                  outbox dispatcher (at-least-once)
db/migrations                forward-only, reviewed
contracts/openapi            generated artifacts
locales                      shared dictionary sources
tests/{Unit,Integration,Contract,Security,E2E}
docs/{adr,plans,superpowers/specs}, .superpowers/sdd/<plan>/ (ledger, briefs, reports)
```

Module references are acyclic; cross-module reads go through published contracts (in-pilot:
Merchandising reads PDM style/BOM projections by contract, never writes PDM tables).

## 4. Foundation contracts

### 4.1 Response envelope (Ruling R-1)
- Success: `{ "data": …, "meta": { … } }` — `meta` carries pagination/cursor when applicable.
- Error: RFC 9457 `application/problem+json` — `type, title, status, detail, code (stable domain code), errors[] (per-field), correlationId`.
- Exactly these two shapes everywhere; no ad-hoc error bodies.

### 4.2 Tenancy (Ruling R-2)
- Tenant + facility context derives **only** from verified JWT claims (`tid`, `fid`) — never from body/query (FND-007).
- Async-local `TenantContext`; every repository query passes through the central guard / EF global query filters.
- Every tenant-owned table: `tenant_id uuid NOT NULL` inside unique keys and composite FKs (constraint-level cross-tenant protection).
- RLS is **deferred** (spec gates it on connection-pool testing); the query guard itself gets direct security tests. Cost-if-wrong: a missed guard leaks within the trust of the API layer — mitigated by §10 security tests.

### 4.3 Concurrency & idempotency
- Optimistic concurrency: `revision int` on mutable aggregates; mutations carry `expectedRevision` / `If-Match`; stale → `409` + stable code (e.g. `ORDER_REVISION_CONFLICT`).
- Idempotent creates/postings: `Idempotency-Key` header → stored first outcome (request-hash + response snapshot), unique per `(tenant_id, endpoint, key)`; replay returns original result; no double write.

### 4.4 State machines (FND-014)
- Transitions are named commands (`POST /{id}/confirm`), guarded at the write; client-provided status is never authoritative; transition history (from, to, actor, reason, at) is preserved.
- Pilot machines: Style `Draft→Approved` (approve); BOM `Draft→Released` (approve, requires lines); CustomerOrder `Draft→Confirmed` (confirm), plus `→Cancelled` from Draft/Confirmed with reason. Invalid transitions → `422` with stable code.

### 4.5 Outbox & audit
- `outbox.outbox_message`: id, type (`pdm.style.master.creation.changed.v1` … per sheet event names), schema version, tenant, aggregate id, actor, correlation id, occurred/recorded UTC, payload JSONB. Written **in the same transaction** as business state. Dispatcher: poll, at-least-once, dedupe by event id; pilot consumer = delivery log.
- `audit.audit_event` (append-only): actor, tenant, facility, command, entity type/id, changed-field set, timestamp, reason, correlation id, approval reference. No credentials, minimal PII, never echoed back.

### 4.6 Values & identifiers
- Money `numeric(18,2)` + explicit `currency char(3)`; quantities `numeric(18,4)` + UOM code. No floats (Ruling R-6).
- UUID PKs; business codes unique per tenant (e.g. `UNIQUE (tenant_id, style_code)`); UTC `timestamptz`; business dates `date`.
- GraphQL of GS1/SSCC crosswalks: **out of pilot scope** (identifier registry is TRC volume).

## 5. Pilot domain model

Aggregate ownership per capability sheets; all tables follow ORG-0003 column baseline
(`id, tenant_id, facility_id?, business key, status, revision, created_at_utc/by, updated_at_utc/by, source_ref?`).

### Organization (ORG-0001/0009/0033; ORG-0017's warehouses deferred with §12)
`org.tenant`, `org.legal_entity`, `org.facility` (code, name, timezone, address), `org.tenant_locale_policy` (enabled locales, default). Seed: 1 tenant, 1 entity, 1 facility.

### Identity (IAM-0041/0049)
`iam.app_user` (email unique per tenant, password hash, display name, active), `iam.role`, `iam.permission` (catalogue rows: `module.resource.action`), `iam.role_permission`, `iam.user_role`. Login issues JWT with `tid/fid/sub/perm` claims. **Deferred:** invitation flow, SSO/federation (IAM-0065), MFA (Ruling R-3 — bootstrap admin is seeded; cost-if-wrong: no self-service join in pilot).

### Product engineering (PDM-0081/0089/0097)
- `pdm.material` (code, name, uom, category) — minimal master for BOM lines.
- `pdm.uom` + conversions subset needed for validation.
- `pdm.style` (code, name, buyer_id, season, category; `Draft→Approved`), `pdm.style_version` (immutable once referenced), `pdm.style_variant` (color × size rows).
- `pdm.bom` (header, per style_version; `Draft→Released`), `pdm.bom_line` (material, qty_per_unit > 0, uom, waste %, alternatives optional).
- Invariants: **released order pins one immutable style revision**; released BOM lines use valid UOMs and effective material revisions; variant matrix rows unique per (style_version, color, size).

### Merchandising (ORD-0121/0145)
- `ord.buyer` (code, name, country, commercial defaults), `ord.buyer_contact` (role-restricted fields — contact data purpose-limited per sheet invariant).
- `ord.customer_order` (code, buyer_id, buyer_po, currency, order date; `Draft→Confirmed→…`), `ord.order_line` (style_id **+ style_version_id snapshot**, price), `ord.size_quantity` (order_line × color/size), `ord.delivery_schedule` (date, qty per line).
- Invariants: ordered size-quantity units reconcile to line totals and variant totals; `buyer_po` unique per (tenant, buyer); confirm requires an Approved style revision; confirm is idempotent and revision-guarded.

### Pilot permission catalogue (seed)
`org.read`, `iam.user.read/manage`, `pdm.style.read/create/edit/approve`, `pdm.bom.read/create/edit/approve`, `pdm.material.read/create`, `ord.buyer.read/create/edit`, `ord.order.read/create/edit/confirm`, `audit.read`. Seeded roles: Admin (all), Merchandiser (PDM/ORD read+create+edit, no approves), Approver (read + approves). Maker-checker: tenant policy flag `require_maker_checker` (default **off**); when on, approve/confirm reject if approver == creator.

## 6. API surface (`/api/v1`, all under authN except `auth/login`)

| Endpoint | Commands |
|---|---|
| `POST /auth/login` | issue JWT (rate-limited) |
| `GET /org/facilities` | list (scoped) |
| `GET+POST /iam/users`, `iam/roles` | manage membership & roles |
| `GET+POST /pdm/styles`, `GET /pdm/styles/{id}` | `POST /{id}/approve` |
| `GET+POST /pdm/materials` | master |
| `GET+POST /pdm/boms`, `GET /pdm/boms/{id}` | `POST /{id}/lines`, `POST /{id}/approve` (release) |
| `GET+POST /ord/buyers`, `GET /ord/buyers/{id}` | buyer profile |
| `GET+POST /ord/customer-orders`, `GET /ord/customer-orders/{id}` | `POST /{id}/confirm`, `POST /{id}/cancel` |

Collection reads: cursor pagination (`pageSize` capped, unknown filters rejected, bind all filters). Writes: `Idempotency-Key` + `expectedRevision`. Errors per R-1 with stable codes (`STYLE_REVISION_CONFLICT`, `BOM_LINE_INVALID_UOM`, `ORDER_VARIANT_MISMATCH`, `DUPLICATE_BUYER_PO`, `IDEMPOTENT_REPLAY`, `PERMISSION_DENIED`, `TENANT_MISMATCH`, …). Events written: the four `.changed.v1` events named in the sheets.

## 7. Frontend

Next.js 16 App Router. Design tokens extracted verbatim from `website-analysis/design-tokens.json`
into a shadcn-style CSS custom-property theme (`docs/design/ui-direction.md` is the synced instruction file):

- Brand `--primary #5d87ff`, `--secondary #49beff`; status success `#13deb9`, warning `#f6b51e`, error `#ef4444`, info `#8754ec` (+ 10–20% tint aliases); surfaces flip `#ffffff ↔ #1c2536`; DM Sans; `--radius: 10px`; controls 40px; cards p-6; sidebar 268px; header 72px; flat (no gradients/shadows except input-sm/dropdown-md).
- Light/dark via `html` class strategy + persisted toggle; **en + bn** via `app/[lang]`, server-loaded dictionaries, `html lang/dir`, CSS logical properties, bidi isolation for mixed-script codes (FND-017/019).
- Screens: login (boxed, BlankLayout), dashboard (stat tiles + recent orders), buyers (list + form), styles (list + detail with variant matrix editor), BOM editor (lines grid, approve), orders (list + capture + detail with confirm), 404. FullLayout shell = sidebar (module nav, active pill) + sticky header (search stub, theme toggle, lang toggle, avatar) + footer credit.
- Every list screen implements loading/empty/error/success; confirm/approve actions show revision conflict errors inline; accessibility floor per playbook §5.5.

## 8. Seed data (FND-028)

Synthetic only: tenant "TeamzLab Demo Ltd.", legal entity + facility (Asia/Dhaka), locales {en, bn} (Ruling R-4), admin user, 3 roles + permission catalogue, 1 buyer with contact, 1 style (3 colors × 4 sizes) Approved, 1 material set + released BOM, 1 Confirmed order. Command: `db reset` = drop schema → re-run migrations → seed. No real worker/buyer data in dev.

## 9. Migrations

Forward-only, one migration per reviewable change, never edited after apply (playbook §4). Initial migration creates all pilot schemas (`org`, `iam`, `pdm`, `ord`, `outbox`, `audit`) + constraints + indexes per §4.6/§5. Live-schema verification by read-only probe after each apply.

## 10. Testing strategy

| Layer | Approach |
|---|---|
| Unit | domain rules: state machines, invariants (variant reconciliation, BOM validation), permission policy |
| Integration | **provided PostgreSQL via throwaway scratch DB** (`rmg_test_<guid>` created from template, migrations applied, dropped at end — no Docker dependency) (Ruling R-5). Covers constraints, transactions, tenant guard, idempotency replay, outbox atomicity |
| Contract | OpenAPI snapshot; every endpoint's happy + negative status codes |
| Security | cross-tenant ID substitution, missing permission, stale revision, replay, mass-assignment, contact-field role restriction |
| E2E | Playwright (MCP Chrome): login → buyer → style → BOM → order → confirm; screenshots light/dark × en/bn for the design gate |

Definition of done per feature (FND-030, pilot-trimmed): invariants + permissions + audit row + locale keys + migration + OpenAPI + error states + tests green on the branch.

## 11. Rulings register (ledger seeds)

| # | Ruling | Cost if wrong |
|---|---|---|
| R-1 | Success envelope `{data, meta}`; errors RFC 9457 problem+json with stable `code` | clients written against ad-hoc shapes break at the next module |
| R-2 | No RLS in pilot; central query guard + EF filters + security tests instead | a missed guard could leak cross-tenant rows inside the API trust boundary |
| R-3 | Bootstrap admin seeded; invitation/SSO/MFA deferred | no self-service onboarding until IAM deep slice |
| R-4 | Pilot locales {en, bn} | more locales = dictionary work later, no structural cost |
| R-5 | Integration tests run scratch DBs on the owner's PostgreSQL instance | tests share fate with the dev DB instance |
| R-6 | `numeric(18,4)` quantities, `numeric(18,2)` money + explicit currency/UOM codes | rounding drift if changed later — migrate before first real postings |

## 12. Out of scope (this drive)

All volumes except ORG/IAM/PDM/ORD pilot subsets; RFQ/quotation (ORD-0129/0137), order change control (ORD-0153), spec/artwork revisions (PDM-0105), operation routes (PDM-0113), SSO (IAM-0065), audit search/export UI (IAM-0073), calendars/shifts UI (ORG-0025), RLS enforcement, GS1/EPCIS, integrations/webhooks/EDI, mobile/offline, analytics, documents, DPP, finance/costing/compliance/traceability.

## 13. Process

Branch flow `feat/* → integration → main` with full regression at the integration gate; conventional commits + configured attribution; plan file `docs/01_Plan.md` (living checklist + done-log); ledger `.superpowers/sdd/foundation-pilot/LEDGER.md`; implementer fan-out only where file-safe (controller owns shared foundation files).
