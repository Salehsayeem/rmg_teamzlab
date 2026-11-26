import type { ComponentType } from "react";
import { DashboardIcon, OrdersIcon, ShirtIcon, UsersIcon } from "@/components/icons";
import type { Dict } from "@/lib/i18n/getDict";

/** One sidebar entry: path under /{lang}, its dict.nav label key, and its icon component. */
export type NavEntry = {
  href: string;
  dictKey: Exclude<keyof Dict["nav"], "label">;
  icon: ComponentType;
};

/**
 * Sidebar nav registry — the single source of truth Sidebar consumes. Later
 * modules append their entries here at merge; routes are the planned paths so
 * a module's page landing only needs the registry row, no Sidebar edit.
 */
export const NAV: NavEntry[] = [
  { href: "/dashboard", dictKey: "dashboard", icon: DashboardIcon },
  { href: "/buyers", dictKey: "buyers", icon: UsersIcon },
  { href: "/styles", dictKey: "styles", icon: ShirtIcon },
  { href: "/orders", dictKey: "orders", icon: OrdersIcon },
];
