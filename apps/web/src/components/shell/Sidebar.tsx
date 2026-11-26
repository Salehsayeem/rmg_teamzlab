"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { NAV } from "@/components/shell/nav";
import type { Dict, Lang } from "@/lib/i18n/getDict";

export function Sidebar({ lang, dict }: { lang: Lang; dict: Dict }) {
  const pathname = usePathname();
  const isActive = (href: string) => pathname === href || pathname.startsWith(`${href}/`);

  return (
    <aside className="sticky top-0 hidden h-screen w-[268px] shrink-0 flex-col border-e border-sidebar-border bg-sidebar px-4 py-5 xl:flex">
      <div className="flex items-center gap-2.5 px-2 pb-5">
        <span
          aria-hidden="true"
          className="flex size-9 items-center justify-center rounded-lg bg-primary text-sm font-semibold text-primary-foreground"
        >
          {dict.app.mark}
        </span>
        <span className="text-base font-semibold tracking-tight">{dict.app.name}</span>
      </div>
      <nav aria-label={dict.nav.label} className="flex flex-col gap-1">
        {NAV.map(({ href, dictKey, icon: Icon }) => {
          const full = `/${lang}${href}`;
          return (
            <Link
              key={href}
              href={full}
              aria-current={isActive(full) ? "page" : undefined}
              className={
                isActive(full)
                  ? "flex h-10 items-center gap-3 rounded-lg bg-sidebar-primary px-3 text-sm font-medium text-sidebar-primary-foreground transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                  : "flex h-10 items-center gap-3 rounded-lg px-3 text-sm text-sidebar-foreground transition-colors hover:bg-sidebar-accent hover:text-sidebar-accent-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
            }
          >
            <span aria-hidden="true" className="shrink-0">
              <Icon />
            </span>
            {dict.nav[dictKey]}
          </Link>
        );
      })}
      </nav>
    </aside>
  );
}
