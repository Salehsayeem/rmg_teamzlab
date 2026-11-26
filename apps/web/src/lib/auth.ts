import { cookies } from "next/headers";
import { SESSION_COOKIE } from "./api";

/**
 * Reads the httpOnly `rmg_session` cookie value (set later by the login proxy
 * route, Task 17). Null when signed out.
 */
export async function getSessionToken(): Promise<string | null> {
  return (await cookies()).get(SESSION_COOKIE)?.value ?? null;
}

/**
 * Decodes the JWT payload's repeated `perm` claim from the session cookie.
 * No signature verification — the API enforces authorization; the web only
 * gates UI affordances (button disabled states, page-level notices).
 */
export async function getPerms(): Promise<string[]> {
  const segment = (await getSessionToken())?.split(".")[1];
  if (!segment) return [];
  try {
    const payload = JSON.parse(Buffer.from(segment, "base64url").toString()) as {
      perm?: unknown;
    };
    if (typeof payload.perm === "string") return [payload.perm];
    return Array.isArray(payload.perm)
      ? payload.perm.filter((p): p is string => typeof p === "string")
      : [];
  } catch {
    return [];
  }
}
