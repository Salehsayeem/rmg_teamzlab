"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { getDict, isLang } from "@/lib/i18n/getDict";

/**
 * Route-level 404 for URLs under a valid [lang] segment (e.g. junk ids like
 * /en/orders/<guid>). Fully unmatched URLs (no lang segment) render
 * global-not-found.tsx instead. Not-found components receive no params, so the
 * locale is read from the first path segment.
 */
export default function NotFound() {
  const seg = usePathname().split("/")[1] ?? "";
  const dict = getDict(isLang(seg) ? seg : "en");
  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-3 bg-background p-6 text-center text-foreground">
      <p aria-hidden="true" className="text-5xl font-semibold tracking-tight text-primary">
        404
      </p>
      <h1 className="text-xl font-semibold">{dict.notFound.title}</h1>
      <p className="max-w-sm text-sm text-sidebar-foreground">{dict.notFound.body}</p>
      <div className="mt-3 flex flex-wrap justify-center gap-3">
        <Link
          href="/en/dashboard"
          hrefLang="en"
          className="flex h-10 items-center rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground transition-colors hover:bg-primary/90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          {dict.notFound.toEn}
        </Link>
        <Link
          href="/bn/dashboard"
          hrefLang="bn"
          className="flex h-10 items-center rounded-lg border border-border px-4 text-sm font-medium text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          {dict.notFound.toBn}
        </Link>
      </div>
    </main>
  );
}
