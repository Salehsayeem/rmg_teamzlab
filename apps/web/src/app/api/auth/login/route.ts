import { cookies } from "next/headers";
import { API_URL, SESSION_COOKIE } from "@/lib/api";

/**
 * Login proxy (R-2): forwards credentials to the API and upgrades the returned
 * access token into the httpOnly `rmg_session` cookie (sameSite lax, lifetime =
 * token expiresInSeconds). Problem+json failures are forwarded as { code,
 * errors[] } with the API status so the client can localize them.
 */
export async function POST(request: Request) {
  const payload = await request.json().catch(() => ({}));

  let res: Response;
  try {
    res = await fetch(`${API_URL}/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
      cache: "no-store",
    });
  } catch {
    return Response.json({ code: "NETWORK", errors: [] }, { status: 502 });
  }

  if (!res.ok) {
    const problem = (await res.json().catch(() => null)) as {
      code?: string;
      errors?: unknown;
    } | null;
    const errors = Array.isArray(problem?.errors)
      ? problem.errors.filter((e): e is string => typeof e === "string")
      : [];
    return Response.json(
      { code: problem?.code ?? "UNKNOWN", errors },
      { status: res.status },
    );
  }

  const envelope = (await res.json().catch(() => null)) as {
    data?: { accessToken?: string; expiresInSeconds?: number };
  } | null;
  const { accessToken, expiresInSeconds } = envelope?.data ?? {};
  if (!accessToken) {
    return Response.json({ code: "UNKNOWN", errors: [] }, { status: 502 });
  }

  const store = await cookies();
  store.set({
    name: SESSION_COOKIE,
    value: accessToken,
    httpOnly: true,
    sameSite: "lax",
    path: "/",
    maxAge: expiresInSeconds ?? 8 * 60 * 60,
    secure: process.env.NODE_ENV === "production",
  });
  return Response.json({ ok: true });
}
