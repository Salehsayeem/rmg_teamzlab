import type { Metadata } from "next";
import "./globals.css";
import { getDict } from "@/lib/i18n/getDict";

export const metadata: Metadata = { title: getDict("en").notFound.title };

/**
 * Global 404 (experimental.globalNotFound). The root layout lives at
 * app/[lang]/layout.tsx so html lang/dir can come from the route segment;
 * unmatched URLs (including "/") bypass layouts and render this full document.
 * No lang segment exists on a 404 URL, so copy uses the default locale.
 */
export default function GlobalNotFound() {
  const dict = getDict("en");
  return (
    <html lang="en" dir="ltr" suppressHydrationWarning>
      <body className="antialiased">
        <main className="flex min-h-screen flex-col items-center justify-center gap-3 bg-background p-6 text-center text-foreground">
          <p aria-hidden="true" className="text-5xl font-semibold tracking-tight text-primary">
            404
          </p>
          <h1 className="text-xl font-semibold">{dict.notFound.title}</h1>
          <p className="max-w-sm text-sm text-sidebar-foreground">{dict.notFound.body}</p>
          <div className="mt-3 flex flex-wrap justify-center gap-3">
            <a
              href="/en/dashboard"
              hrefLang="en"
              className="flex h-10 items-center rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
            >
              {dict.notFound.toEn}
            </a>
            <a
              href="/bn/dashboard"
              hrefLang="bn"
              className="flex h-10 items-center rounded-lg border border-border px-4 text-sm font-medium text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
            >
              {dict.notFound.toBn}
            </a>
          </div>
        </main>
      </body>
    </html>
  );
}
