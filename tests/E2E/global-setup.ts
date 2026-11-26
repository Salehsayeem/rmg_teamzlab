import { execSync, spawn, type ChildProcess } from "node:child_process";
import { existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import net from "node:net";
import path from "node:path";
import { API_URL, WEB_URL } from "./playwright.config";

/**
 * Global setup: fresh scratch database → API (Development boot installs the
 * pilot seed + demo dataset) → web (production build + start). Everything this
 * starts is recorded in .run/servers.json and torn down by global-teardown —
 * plus an exit/SIGINT net while setup itself is in flight, so a crash or Ctrl-C
 * mid-setup leaves nothing behind (the net is removed once setup succeeds;
 * from there global-teardown owns the cleanup).
 *
 * Scratch DB comes from the repo-root (worktree) .env, ConnectionStrings__Default,
 * which MUST point at rmg_t23_scratch — the guard below refuses to run against
 * anything else (the dev database rmg_teamzlab is never touched).
 */

const ROOT = path.resolve(__dirname, "..", ".."); // tests/E2E → repo root (the worktree)
const WEB_APP = path.join(ROOT, "apps", "web");
const RUN_DIR = path.join(__dirname, ".run");
const SERVERS_FILE = path.join(RUN_DIR, "servers.json");

const webPort = new URL(WEB_URL).port || "80";
const apiPort = new URL(API_URL).port || "80";

function readEnv(): Record<string, string> {
  const file = path.join(ROOT, ".env");
  if (!existsSync(file))
    throw new Error(`${file} is missing — create it with ConnectionStrings__Default pointing at rmg_t23_scratch.`);
  const env: Record<string, string> = {};
  for (const line of readFileSync(file, "utf8").split(/\r?\n/)) {
    const m = /^([A-Za-z0-9_]+)=(.*)$/.exec(line.trim());
    if (m) env[m[1]] = m[2];
  }
  return env;
}

type Conn = { host: string; port: string; user: string; password: string; database: string };

function parseConn(cs: string): Conn {
  const get = (k: string) => new RegExp(`${k}=([^;]*)`).exec(cs)?.[1] ?? "";
  const conn = {
    host: get("Host") || "localhost",
    port: get("Port") || "5432",
    user: get("Username") || "postgres",
    password: get("Password"),
    database: get("Database"),
  };
  if (conn.database !== "rmg_t23_scratch")
    throw new Error(`Refusing to run: the scratch .env must target rmg_t23_scratch, got "${conn.database}".`);
  return conn;
}

function findPsql(): string {
  const base = "C:/Program Files/PostgreSQL";
  if (existsSync(base)) {
    for (const v of readdirSync(base).sort().reverse()) {
      const exe = path.join(base, v, "bin", "psql.exe");
      if (existsSync(exe)) return exe;
    }
  }
  return "psql";
}

function psql(conn: Conn, db: string, sql: string): string {
  return execSync(
    `"${findPsql()}" -h ${conn.host} -p ${conn.port} -U ${conn.user} -d ${db} -v ON_ERROR_STOP=1 -c "${sql}"`,
    { env: { ...process.env, PGPASSWORD: conn.password }, stdio: ["ignore", "pipe", "pipe"], encoding: "utf8" },
  );
}

function killTree(pid: number): void {
  try {
    if (process.platform === "win32") execSync(`taskkill /PID ${pid} /T /F`, { stdio: "ignore" });
    else process.kill(-pid, "SIGKILL");
  } catch {
    /* already gone */
  }
}

/** Idempotent: safe to call from globalTeardown, the crash nets, and the
 * aborted-run self-heal at setup start. The servers.json ledger is the source
 * of truth; it is removed only after everything is down and the DB dropped. */
export function teardown(): void {
  if (existsSync(SERVERS_FILE)) {
    const { api, web, conn } = JSON.parse(readFileSync(SERVERS_FILE, "utf8")) as {
      api?: number;
      web?: number;
      conn?: Conn;
    };
    if (api) killTree(api);
    if (web) killTree(web);
    if (conn) {
      try {
        psql(conn, "postgres", `DROP DATABASE IF EXISTS ${conn.database} WITH (FORCE)`);
      } catch {
        /* best effort — the scratch DB may already be gone */
      }
    }
  }
  rmSync(RUN_DIR, { recursive: true, force: true });
}

async function waitReady(url: string, label: string, timeoutMs: number): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  let last = "";
  while (Date.now() < deadline) {
    try {
      const res = await fetch(url);
      if (res.ok) return;
      last = `HTTP ${res.status}`;
    } catch (e) {
      last = e instanceof Error ? e.message.split("\n")[0] : String(e);
    }
    await new Promise((r) => setTimeout(r, 1000));
  }
  throw new Error(`${label} never became ready at ${url} (last: ${last})`);
}

/** A port serving something this harness did not start would make readiness
 * checks pass against a foreign/zombie server — refuse instead. */
async function assertPortFree(port: string, label: string): Promise<void> {
  await new Promise<void>((resolve, reject) => {
    const probe = net.createServer();
    probe.once("error", () =>
      reject(new Error(`${label} port ${port} is already in use — free it or override E2E_${label.toUpperCase()}_URL.`)));
    probe.listen(Number(port), () => probe.close(resolve));
  });
}

function spawnServer(cmd: string, args: string[], cwd: string, extraEnv: Record<string, string>): ChildProcess {
  const child = spawn(cmd, args, {
    cwd,
    env: { ...process.env, ...extraEnv },
    stdio: "ignore",
    detached: process.platform !== "win32", // own process group so killTree can take the tree
  });
  if (!child.pid) throw new Error(`${cmd} failed to start`);
  return child;
}

/** The ledger is written incrementally so the teardown net knows about every
 * server that exists, even when a later setup step fails. */
function writeLedger(entry: { api?: number; web?: number; conn: Conn }): void {
  mkdirSync(RUN_DIR, { recursive: true });
  writeFileSync(SERVERS_FILE, JSON.stringify(entry));
}

export default async function globalSetup(): Promise<void> {
  const env = readEnv();
  const conn = parseConn(env["ConnectionStrings__Default"] ?? "");
  mkdirSync(RUN_DIR, { recursive: true });

  // Self-heal a previous aborted run (ledger present ⇒ its teardown never ran).
  if (existsSync(SERVERS_FILE)) teardown();
  await assertPortFree(apiPort, "API");
  await assertPortFree(webPort, "web");

  // Crash nets — only while setup is in flight (removed on success below).
  process.on("exit", teardown);
  process.on("SIGINT", () => process.exit(130));
  process.on("SIGTERM", () => process.exit(143));

  try {
    // Fresh scratch DB every run: drop leftovers, then let migrations recreate it.
    psql(conn, "postgres", `DROP DATABASE IF EXISTS ${conn.database} WITH (FORCE)`);
    execSync("dotnet restore src/Api", { cwd: ROOT, stdio: "inherit" }); // fresh checkouts have no obj/
    execSync("dotnet ef database update --project src/Database --context RmgDbContext", {
      cwd: ROOT,
      stdio: "inherit",
      env: { ...process.env, ASPNETCORE_ENVIRONMENT: "Development" },
    });

    // API: Development env (explicit here; --no-launch-profile keeps ports deterministic)
    // installs the pilot seed + spec §8 demo dataset idempotently on boot.
    const api = spawnServer("dotnet", ["run", "--project", "src/Api", "--no-launch-profile", "--urls", API_URL], ROOT, {
      ASPNETCORE_ENVIRONMENT: "Development",
    });
    writeLedger({ api: api.pid, conn });
    await waitReady(`${API_URL}/`, "API", 180_000);

    // Web: production build + start (next dev wedges under parallel tabs — never used
    // here). next's bin runs under plain node — npm.cmd would trip Node's spawn EINVAL.
    if (!existsSync(path.join(WEB_APP, "node_modules")))
      execSync("npm ci --no-audit --no-fund", { cwd: WEB_APP, stdio: "inherit" }); // fresh checkout
    if (process.env.E2E_SKIP_WEB_BUILD !== "1")
      execSync("npm run build", { cwd: WEB_APP, stdio: "inherit" });
    const nextBin = path.join(WEB_APP, "node_modules", "next", "dist", "bin", "next");
    const web = spawnServer(process.execPath, [nextBin, "start", "-p", webPort], WEB_APP, {
      NEXT_PUBLIC_API_URL: `${API_URL}/api/v1`,
    });
    writeLedger({ api: api.pid, web: web.pid, conn });
    await waitReady(`${WEB_URL}/en/login`, "web", 120_000);
  } finally {
    // Success: hand ownership to global-teardown. Failure: the 'exit' net (still
    // registered) cleans up as this process unwinds.
    process.off("exit", teardown);
  }
}
