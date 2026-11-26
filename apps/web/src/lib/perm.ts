import { getSessionToken } from "./auth";

/**
 * Permission check for server components/actions: decodes the session JWT's
 * payload segment (base64url, unverified — the API re-checks every request) and
 * looks for the repeated `perm` claim array. Null/invalid token -> false.
 */
export async function can(code: string): Promise<boolean> {
  const token = await getSessionToken();
  if (!token) return false;
  try {
    const payload = JSON.parse(Buffer.from(token.split(".")[1] ?? "", "base64url").toString()) as {
      perm?: unknown;
    };
    return Array.isArray(payload.perm) && payload.perm.includes(code);
  } catch {
    return false;
  }
}
