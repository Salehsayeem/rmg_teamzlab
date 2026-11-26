import { NextResponse, type NextRequest } from "next/server";

// Same constant as SESSION_COOKIE in lib/api.ts — duplicated because proxy files
// must not import modules that use next/headers.
const SESSION_COOKIE = "rmg_session";

const LANGS = ["en", "bn"];

/**
 * Session gate: any /[lang]/... route except /[lang]/login requires the session
 * cookie (presence only — no server-side token validation in the proxy, per plan).
 * API routes and static assets are excluded via the matcher below.
 */
export function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const lang = pathname.split("/")[1];
  if (!LANGS.includes(lang) || pathname === `/${lang}/login`) return NextResponse.next();
  if (request.cookies.has(SESSION_COOKIE)) return NextResponse.next();
  return NextResponse.redirect(new URL(`/${lang}/login`, request.url));
}

export const config = {
  // Everything except api routes, Next internals and metadata/static files.
  matcher: "/((?!api|_next/static|_next/image|favicon.ico|sitemap.xml|robots.txt|.*\\..*).*)",
};
