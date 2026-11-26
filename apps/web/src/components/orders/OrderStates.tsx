import Link from "next/link";
import type { ReactNode } from "react";
import type { Dict, Lang } from "@/lib/i18n/getDict";

/** OrderDto as the API returns it (lines carry their size quantities and delivery schedules). */
export type OrderDto = {
  id: string;
  code: string;
  buyerId: string;
  buyerPo: string;
  currency: string;
  orderDate: string;
  status: string;
  revision: number;
  lines: {
    id: string;
    lineNo: number;
    styleId: string;
    styleVersionId: string;
    price: number;
    lineQty: number;
    sizes: { colorCode: string; sizeCode: string; qty: number }[];
    schedules: { shipDate: string; qty: number }[];
  }[];
};

export const DEFAULT_PAGE_SIZE = 20;
/** History marker for "this page had no cursor" (the first page) in the hist stack. */
export const NO_CURSOR = "~";

/** Loading state: skeleton rows mirroring the orders table (dashboard pattern). */
export function OrdersSkeleton({ label }: { label: string }) {
  return (
    <div role="status" aria-label={label} className="flex flex-col gap-6">
      <div className="h-10 w-40 animate-pulse rounded-lg bg-muted" />
      <div className="flex flex-col gap-2">
        {[0, 1, 2, 3].map((i) => (
          <div key={i} className="flex h-10 items-center gap-4">
            <span className="h-4 w-24 animate-pulse rounded bg-muted" />
            <span className="h-4 w-48 animate-pulse rounded bg-muted" />
            <span className="h-4 w-20 animate-pulse rounded bg-muted" />
            <span className="ms-auto h-4 w-20 animate-pulse rounded bg-muted" />
          </div>
        ))}
      </div>
    </div>
  );
}

export function EmptyState({ title, body, icon }: { title: string; body: string; icon: ReactNode }) {
  return (
    <div className="flex flex-col items-center gap-3 py-10 text-center">
      <span aria-hidden="true" className="flex size-12 items-center justify-center rounded-full bg-muted">
        {icon}
      </span>
      <p className="text-sm font-medium">{title}</p>
      <p className="max-w-sm text-sm text-sidebar-foreground">{body}</p>
    </div>
  );
}

export function ErrorState({ title, body, icon, retryHref, retryLabel }: {
  title: string;
  body: string;
  icon: ReactNode;
  retryHref: string;
  retryLabel: string;
}) {
  return (
    <div role="alert" className="flex flex-col items-center gap-3 py-10 text-center">
      <span aria-hidden="true" className="flex size-12 items-center justify-center rounded-full bg-lighterror text-error">
        {icon}
      </span>
      <p className="text-sm font-medium">{title}</p>
      <p className="max-w-sm text-sm text-sidebar-foreground">{body}</p>
      <Link
        href={retryHref}
        className="mt-1 flex h-10 items-center rounded-lg border border-border px-4 text-sm font-medium text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      >
        {retryLabel}
      </Link>
    </div>
  );
}

const STATUS_BADGE: Record<string, string> = {
  Draft: "bg-muted",
  Confirmed: "bg-lightsuccess",
  Cancelled: "bg-lighterror",
};
const STATUS_DOT: Record<string, string> = {
  Draft: "bg-muted-foreground",
  Confirmed: "bg-success",
  Cancelled: "bg-error",
};
const STATUS_TEXT: Record<string, string> = { Cancelled: "text-error" };

/** Status badge tint: Draft neutral, Confirmed success, Cancelled destructive; unknown -> neutral. */
export function OrderStatusBadge({ status, dict }: { status: string; dict: Dict }) {
  const label = dict.orders.status[status.toLowerCase() as keyof typeof dict.orders.status] ?? status;
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium text-sidebar-foreground ${STATUS_TEXT[status] ?? ""} ${STATUS_BADGE[status] ?? "bg-muted"}`}
    >
      <span aria-hidden="true" className={`size-1.5 rounded-full ${STATUS_DOT[status] ?? "bg-muted-foreground"}`} />
      {label}
    </span>
  );
}

/** URL for an orders-list page: pageSize + optional keyset cursor + visited-cursor history stack. */
export function ordersHref(lang: Lang, pageSize: number, cursor?: string, hist?: string) {
  const q = new URLSearchParams({ pageSize: String(pageSize) });
  if (cursor) q.set("cursor", cursor);
  if (hist) q.set("hist", hist);
  return `/${lang}/orders?${q}`;
}
