"use client";

import { useEffect, useRef, useState } from "react";
import { usePathname } from "next/navigation";
import { NAV } from "@/components/shell/nav";
import type { Dict, Lang } from "@/lib/i18n/getDict";

// Local glyphs — icons.tsx is outside this module's ownership.
function MenuIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.8}
      strokeLinecap="round"
      strokeLinejoin="round"
      className="size-[18px]"
      aria-hidden="true"
    >
      <path d="M4 6h16" />
      <path d="M4 12h16" />
      <path d="M4 18h16" />
    </svg>
  );
}

function CloseIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.8}
      strokeLinecap="round"
      strokeLinejoin="round"
      className="size-4"
      aria-hidden="true"
    >
      <path d="M18 6 6 18" />
      <path d="m6 6 12 12" />
    </svg>
  );
}

/**
 * Mobile nav below xl (the desktop sidebar's breakpoint): header hamburger
 * opening an overlay drawer over the same NAV registry with the sidebar's
 * active-link highlighting. Focus wiring mirrors the BOM confirm dialog.
 */
export function MobileNav({ lang, dict }: { lang: Lang; dict: Dict }) {
  const pathname = usePathname();
  const [open, setOpen] = useState(false);
  const triggerRef = useRef<HTMLButtonElement>(null);
  // Focus returns to the hamburger in an effect after the close commit (a
  // direct focus() call in a close handler is dead — see BomForms).
  const restoreFocusRef = useRef(false);
  useEffect(() => {
    if (!open && restoreFocusRef.current) {
      restoreFocusRef.current = false;
      triggerRef.current?.focus();
    }
  }, [open]);
  useEffect(() => {
    if (open) {
      document.body.style.overflow = "hidden";
      return () => {
        document.body.style.overflow = "";
      };
    }
  }, [open]);

  const close = () => {
    restoreFocusRef.current = true;
    setOpen(false);
  };
  const isActive = (href: string) => pathname === href || pathname.startsWith(`${href}/`);

  return (
    <div className="xl:hidden">
      <button
        type="button"
        ref={triggerRef}
        aria-label={dict.mobileNav.open}
        aria-expanded={open}
        aria-controls="mobile-nav-drawer"
        onClick={() => setOpen(true)}
        className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg border border-border text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      >
        <MenuIcon />
      </button>
      {open && (
        <div
          className="fixed inset-0 z-50 flex xl:hidden"
          onKeyDown={(event) => {
            if (event.key === "Escape") close();
          }}
        >
          <div aria-hidden="true" onClick={close} className="min-w-0 flex-1 bg-foreground/50" />
          <div
            id="mobile-nav-drawer"
            role="dialog"
            aria-modal="true"
            aria-label={dict.nav.label}
            className="flex h-full w-[268px] shrink-0 flex-col border-e border-sidebar-border bg-sidebar px-4 py-5"
          >
            <div className="flex items-center gap-2.5 px-2 pb-5">
              <span
                aria-hidden="true"
                className="flex size-9 items-center justify-center rounded-lg bg-primary text-sm font-semibold text-primary-foreground"
              >
                {dict.app.mark}
              </span>
              <span className="min-w-0 flex-1 truncate text-base font-semibold tracking-tight">{dict.app.name}</span>
              <button
                type="button"
                autoFocus
                aria-label={dict.mobileNav.close}
                onClick={close}
                className="flex size-9 shrink-0 items-center justify-center rounded-lg border border-border text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
              >
                <CloseIcon />
              </button>
            </div>
            <nav aria-label={dict.nav.label} className="flex flex-col gap-1">
              {NAV.map(({ href, dictKey, icon: Icon }) => {
                const full = `/${lang}${href}`;
                return (
                  <a
                    key={href}
                    href={full}
                    aria-current={isActive(full) ? "page" : undefined}
                    onClick={close}
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
                  </a>
                );
              })}
            </nav>
          </div>
        </div>
      )}
    </div>
  );
}
