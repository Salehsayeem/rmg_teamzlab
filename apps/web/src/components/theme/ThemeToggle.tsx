"use client";

import { MoonIcon, SunIcon } from "@/components/icons";
import { useTheme } from "./ThemeProvider";

export function ThemeToggle({ label }: { label: string }) {
  const { toggle } = useTheme();
  return (
    <button
      type="button"
      onClick={toggle}
      aria-label={label}
      title={label}
      className="flex size-10 shrink-0 items-center justify-center rounded-lg border border-border bg-background text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
    >
      <span aria-hidden="true" className="dark:hidden">
        <MoonIcon />
      </span>
      <span aria-hidden="true" className="hidden dark:block">
        <SunIcon />
      </span>
    </button>
  );
}
