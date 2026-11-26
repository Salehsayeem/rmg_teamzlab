import Link from "next/link";
import type { ReactNode } from "react";
import type { Dict, Lang } from "@/lib/i18n/getDict";

/** BuyerDto as the API returns it (email/phone stripped server-side without ord.buyer.manage). */
export type BuyerDto = {
  id: string;
  code: string;
  name: string;
  country: string;
  currency: string;
  status: string;
  revision: number;
  contacts: { name: string; email?: string | null; phone?: string | null }[];
};

export const DEFAULT_PAGE_SIZE = 20;
/** History marker for "this page had no cursor" (the first page) in the hist stack. */
export const NO_CURSOR = "~";

/** Loading state: skeleton rows mirroring the buyers table (dashboard pattern). */
export function BuyersSkeleton({ label }: { label: string }) {
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

const STATUS_BADGE: Record<string, string> = { Active: "bg-lightsuccess" };
const STATUS_DOT: Record<string, string> = { Active: "bg-success" };

/** Status badge tinted like the dashboard placeholder; unknown statuses fall back to neutral. */
export function StatusBadge({ status, dict }: { status: string; dict: Dict }) {
  const label = status === "Active" ? dict.buyers.status.active : status;
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium text-sidebar-foreground ${STATUS_BADGE[status] ?? "bg-muted"}`}
    >
      <span aria-hidden="true" className={`size-1.5 rounded-full ${STATUS_DOT[status] ?? "bg-muted-foreground"}`} />
      {label}
    </span>
  );
}

/** URL for a buyers-list page: pageSize + optional keyset cursor + visited-cursor history stack. */
export function buyersHref(lang: Lang, pageSize: number, cursor?: string, hist?: string) {
  const q = new URLSearchParams({ pageSize: String(pageSize) });
  if (cursor) q.set("cursor", cursor);
  if (hist) q.set("hist", hist);
  return `/${lang}/buyers?${q}`;
}
