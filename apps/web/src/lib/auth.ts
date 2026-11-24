import { cookies } from "next/headers";
import { SESSION_COOKIE } from "./api";

/**
 * Reads the httpOnly `rmg_session` cookie value (set later by the login proxy
 * route, Task 17). Null when signed out.
 */
export async function getSessionToken(): Promise<string | null> {
  return (await cookies()).get(SESSION_COOKIE)?.value ?? null;
}
