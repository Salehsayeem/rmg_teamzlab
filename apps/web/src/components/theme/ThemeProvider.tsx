"use client";

import { createContext, useCallback, useContext, useSyncExternalStore } from "react";

export type Theme = "light" | "dark";
export const THEME_STORAGE_KEY = "rmg-theme";

type ThemeContextValue = { theme: Theme; toggle: () => void };

const ThemeContext = createContext<ThemeContextValue>({ theme: "light", toggle: () => {} });

/*
 * Class strategy on <html> (`dark` class), persisted in localStorage `rmg-theme`.
 * The pre-paint inline script in app/[lang]/layout.tsx seeds the class before
 * paint, so the DOM class is the source of truth; the store below just mirrors
 * it for React consumers.
 */
const listeners = new Set<() => void>();

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function getSnapshot(): Theme {
  return document.documentElement.classList.contains("dark") ? "dark" : "light";
}

function getServerSnapshot(): Theme {
  return "light";
}

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const theme = useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);

  const toggle = useCallback(() => {
    const next: Theme = document.documentElement.classList.contains("dark") ? "light" : "dark";
    document.documentElement.classList.toggle("dark", next === "dark");
    try {
      localStorage.setItem(THEME_STORAGE_KEY, next);
    } catch {
      // private mode / storage disabled — theme still applies for the session
    }
    listeners.forEach((notify) => notify());
  }, []);

  return <ThemeContext.Provider value={{ theme, toggle }}>{children}</ThemeContext.Provider>;
}

export function useTheme() {
  return useContext(ThemeContext);
}
