import type { ReactNode } from "react";
import {
  AlertIcon,
  InboxIcon,
  LayersIcon,
  OrdersIcon,
  ShirtIcon,
  UsersIcon,
} from "@/components/icons";
import { getDict, type Dict, type Lang } from "@/lib/i18n/getDict";

/**
 * Static fixture so every dashboard UI state can be previewed until the API
 * tasks land: "empty" renders by default (no fake data), "loading" shows the
 * skeleton, "error" and "success" render their fixtures. Later tasks replace
 * this switch with real apiFetch calls.
 */
const FIXTURE_STATE: "loading" | "empty" | "error" | "success" = "empty";

const SAMPLE_ROWS: {
  order: string;
  buyer: string;
  qty: string;
  status: keyof Dict["dashboard"]["status"];
}[] = [
  { order: "ORD-001", buyer: "Northline Apparel", qty: "12,480", status: "confirmed" },
  { order: "ORD-002", buyer: "Baltic Fashion Group", qty: "8,950", status: "inProduction" },
  { order: "ORD-003", buyer: "Cedar & Co", qty: "23,100", status: "pending" },
];

function StatTile({
  label,
  hint,
  value,
  tint,
  icon,
}: {
  label: string;
  hint: string;
  value: string;
  tint: string;
  icon: ReactNode;
}) {
  return (
    <div className="rounded-lg border border-border bg-card p-6">
      <div className="flex items-center gap-3">
        <span aria-hidden="true" className={`flex size-10 items-center justify-center rounded-lg ${tint}`}>
          {icon}
        </span>
        <span className="text-sm font-medium">{label}</span>
      </div>
      <p className="mt-3 text-xl font-semibold tracking-tight">{value}</p>
      <p className="mt-1 text-xs text-sidebar-foreground">{hint}</p>
    </div>
  );
}

/** Loading state: skeleton markup mirroring the recent-orders table rows. */
function DashboardSkeleton({ label }: { label: string }) {
  return (
    <div role="status" aria-label={label} className="flex flex-col gap-2">
      {[0, 1, 2, 3].map((i) => (
        <div key={i} className="flex h-10 items-center gap-4">
          <span className="size-4 animate-pulse rounded bg-muted" />
          <span className="h-4 w-28 animate-pulse rounded bg-muted" />
          <span className="ms-auto h-4 w-16 animate-pulse rounded bg-muted" />
        </div>
      ))}
    </div>
  );
}

function EmptyState({ title, body, icon }: { title: string; body: string; icon: ReactNode }) {
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

function ErrorState({ title, body, icon }: { title: string; body: string; icon: ReactNode }) {
  return (
    <div role="alert" className="flex flex-col items-center gap-3 py-10 text-center">
      <span aria-hidden="true" className="flex size-12 items-center justify-center rounded-full bg-lighterror text-error">
        {icon}
      </span>
      <p className="text-sm font-medium">{title}</p>
      <p className="max-w-sm text-sm text-sidebar-foreground">{body}</p>
    </div>
  );
}

const STATUS_BADGE: Record<keyof Dict["dashboard"]["status"], string> = {
  confirmed: "bg-lightsuccess",
  inProduction: "bg-lightprimary",
  pending: "bg-lightwarning",
};
const STATUS_DOT: Record<keyof Dict["dashboard"]["status"], string> = {
  confirmed: "bg-success",
  inProduction: "bg-primary",
  pending: "bg-warning",
};

function OrdersTable({
  rows,
  headers,
  statuses,
}: {
  rows: typeof SAMPLE_ROWS;
  headers: Dict["dashboard"]["table"];
  statuses: Dict["dashboard"]["status"];
}) {
  return (
    <table className="w-full border-collapse text-sm">
      <thead>
        <tr className="border-b border-border text-start">
          <th scope="col" className="px-2 py-2 text-start font-medium">{headers.order}</th>
          <th scope="col" className="px-2 py-2 text-start font-medium">{headers.buyer}</th>
          <th scope="col" className="px-2 py-2 text-start font-medium">{headers.qty}</th>
          <th scope="col" className="px-2 py-2 text-start font-medium">{headers.status}</th>
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          <tr key={row.order} className="border-b border-border last:border-b-0">
            <td dir="auto" className="px-2 py-2.5 font-medium">{row.order}</td>
            <td dir="auto" className="px-2 py-2.5 text-sidebar-foreground">{row.buyer}</td>
            <td className="px-2 py-2.5 text-sidebar-foreground">{row.qty}</td>
            <td className="px-2 py-2.5">
              <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium text-sidebar-foreground ${STATUS_BADGE[row.status]}`}>
                <span aria-hidden="true" className={`size-1.5 rounded-full ${STATUS_DOT[row.status]}`} />
                {statuses[row.status]}
              </span>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

export default async function DashboardPage({ params }: { params: Promise<{ lang: Lang }> }) {
  const { lang } = await params;
  const dict = getDict(lang);
  const { dashboard: d } = dict;

  const tiles = [
    { ...d.stats.buyers, tint: "bg-lightprimary text-primary", icon: <UsersIcon /> },
    { ...d.stats.styles, tint: "bg-lightsecondary text-secondary", icon: <ShirtIcon /> },
    { ...d.stats.boms, tint: "bg-lightsuccess text-success", icon: <LayersIcon /> },
    { ...d.stats.orders, tint: "bg-lightwarning text-warning", icon: <OrdersIcon /> },
  ];

  return (
    <div className="flex flex-col gap-6">
      <header>
        <h1 className="text-xl font-semibold tracking-tight">{d.title}</h1>
        <p className="mt-1 text-sm text-sidebar-foreground">{d.subtitle}</p>
      </header>
      <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 xl:grid-cols-4">
        {tiles.map((tile) => (
          <StatTile
            key={tile.label}
            label={tile.label}
            hint={tile.hint}
            value={dict.common.valuePlaceholder}
            tint={tile.tint}
            icon={tile.icon}
          />
        ))}
      </div>
      <section aria-labelledby="recent-orders-title" className="rounded-lg border border-border bg-card p-6">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h2 id="recent-orders-title" className="text-base font-semibold">{d.recentOrders.title}</h2>
          <p className="text-sm text-sidebar-foreground">{d.recentOrders.hint}</p>
        </div>
        <div className="mt-4">
          {FIXTURE_STATE === "loading" && <DashboardSkeleton label={d.states.loading} />}
          {FIXTURE_STATE === "empty" && (
            <EmptyState title={d.states.empty} body={d.states.emptyBody} icon={<InboxIcon />} />
          )}
          {FIXTURE_STATE === "error" && (
            <ErrorState title={d.states.error} body={d.states.errorBody} icon={<AlertIcon />} />
          )}
          {FIXTURE_STATE === "success" && (
            <OrdersTable rows={SAMPLE_ROWS} headers={d.table} statuses={d.status} />
          )}
        </div>
      </section>
    </div>
  );
}
