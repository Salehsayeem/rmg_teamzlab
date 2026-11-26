import { cookies } from "next/headers";
import { SESSION_COOKIE } from "@/lib/api";

/** Clears the session cookie; the JWT simply expires (no API revoke endpoint in the pilot). */
export async function POST() {
  const store = await cookies();
  store.set({
    name: SESSION_COOKIE,
    value: "",
    httpOnly: true,
    sameSite: "lax",
    path: "/",
    maxAge: 0,
  });
  return Response.json({ ok: true });
}
