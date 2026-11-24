"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { SearchIcon, UserIcon } from "@/components/icons";
import { ThemeToggle } from "@/components/theme/ThemeToggle";
import type { Dict, Lang } from "@/lib/i18n/getDict";

export function Header({ lang, dict }: { lang: Lang; dict: Dict }) {
  const pathname = usePathname();
  const other: Lang = lang === "en" ? "bn" : "en";
  const langHref = `/${other}${pathname.slice(lang.length + 1)}`;

  return (
    <header className="sticky top-0 z-10 flex h-[72px] shrink-0 items-center gap-4 border-b border-border bg-background px-6">
      <form role="search" className="min-w-0 flex-1">
        <label htmlFor="global-search" className="sr-only">
          {dict.header.searchLabel}
        </label>
        <div className="relative max-w-[320px]">
          <span
            aria-hidden="true"
            className="pointer-events-none absolute start-3 top-1/2 -translate-y-1/2 text-sidebar-foreground"
          >
            <SearchIcon />
          </span>
          <input
            id="global-search"
            type="search"
            placeholder={dict.header.searchPlaceholder}
            className="h-10 w-full rounded-lg border border-input bg-background ps-9 pe-3 text-sm text-foreground placeholder:text-sidebar-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          />
        </div>
      </form>
      <div className="ms-auto flex shrink-0 items-center gap-2">
        <Link
          href={langHref}
          hrefLang={other}
          aria-label={dict.header.langToggle}
          title={dict.header.langToggle}
          className="flex h-10 items-center rounded-lg border border-border px-3 text-sm font-medium text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          {dict.header.langSwitchTo}
        </Link>
        <ThemeToggle label={dict.header.themeToggle} />
        <button
          type="button"
          aria-label={dict.header.avatar}
          title={dict.header.avatar}
          className="flex size-10 shrink-0 items-center justify-center rounded-full border border-border text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          <UserIcon />
        </button>
      </div>
    </header>
  );
}
