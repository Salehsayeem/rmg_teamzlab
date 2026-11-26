import { readFileSync } from "node:fs";
import path from "node:path";
import { API_URL } from "./playwright.config";

/** Shared harness helpers: the API base and the session token taken from the
 * storage state the flow spec's UI login saved (no extra login calls — the
 * login rate limiter allows 10/min/IP). */
export const API_V1 = `${API_URL}/api/v1`;
export const STATE_FILE = path.join(__dirname, ".run", "state.json");

export function sessionToken(): string {
  const state = JSON.parse(readFileSync(STATE_FILE, "utf8")) as {
    origins: { localStorage: { name: string; value: string }[] }[];
    cookies: { name: string; value: string }[];
  };
  // The web's session cookie holds the raw JWT (rmg_session, httpOnly).
  return state.cookies.find((c) => c.name === "rmg_session")?.value ?? "";
}

export async function apiGet(path: string): Promise<Response> {
  return fetch(`${API_V1}${path}`, { headers: { Authorization: `Bearer ${sessionToken()}` } });
}

export async function apiPost(path: string, body?: unknown, idempotencyKey?: string): Promise<Response> {
  return fetch(`${API_V1}${path}`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${sessionToken()}`,
      "Content-Type": "application/json",
      ...(idempotencyKey ? { "Idempotency-Key": idempotencyKey } : {}),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
}

/** Envelope unwrap (R-1: { data, meta }). */
export async function envelope<T>(res: Response): Promise<T> {
  expectOk(res);
  return ((await res.json()) as { data: T }).data;
}

export async function expectOk(res: Response): Promise<void> {
  if (!res.ok) throw new Error(`${res.url} → ${res.status}: ${await res.text()}`);
}
