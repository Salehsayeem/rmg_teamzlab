import { Suspense } from "react";
import type { ReactNode } from "react";
import { AlertIcon, InboxIcon, OrdersIcon, ShirtIcon, UsersIcon } from "@/components/icons";
import { EmptyState, ErrorState, type BuyerDto } from "@/components/buyers/BuyerStates";
import { apiFetch } from "@/lib/api";
import { getSessionToken } from "@/lib/auth";
import { getDict, type Dict, type Lang } from "@/lib/i18n/getDict";

/** StyleDto as GET /pdm/styles returns it — the list is unpaginated, so data.length is the exact count. */
type StyleDto = { id: string };

/** OrderDto as GET /ord/customer-orders returns it; amount derives from lines (Σ price × lineQty). */
type OrderDto = {
  id: string;
  code: string;
  buyerId: string;
  currency: string;
  orderDate: string;
  status: string;
  lines: { price: number; lineQty: number }[];
};

/** Loading state: skeleton mirroring the tile row + recent-orders table (buyers pattern). */
function DashboardSkeleton({ label }: { label: string }) {
  return (
    <div role="status" aria-label={label} className="flex flex-col gap-6">
      <div className="grid grid-cols-1 gap-6 sm:grid-cols-3">
        {[0, 1, 2].map((i) => (
          <div key={i} className="h-[118px] animate-pulse rounded-lg bg-muted" />
        ))}
      </div>
      <div className="flex flex-col gap-2">
        {[0, 1, 2, 3].map((i) => (
          <div key={i} className="flex h-10 items-center gap-4">
            <span className="h-4 w-28 animate-pulse rounded bg-muted" />
            <span className="h-4 w-48 animate-pulse rounded bg-muted" />
            <span className="h-4 w-28 animate-pulse rounded bg-muted" />
            <span className="ms-auto h-4 w-24 animate-pulse rounded bg-muted" />
          </div>
        ))}
      </div>
    </div>
  );
}

/** Order status badge tinted like the buyers badge; unknown statuses fall back to neutral. */
const ORDER_BADGE: Record<string, string> = {
  Draft: "bg-lightwarning",
  Confirmed: "bg-lightsuccess",
  Cancelled: "bg-lighterror",
};
const ORDER_DOT: Record<string, string> = {
  Draft: "bg-warning",
  Confirmed: "bg-success",
  Cancelled: "bg-error",
};

function OrderStatusBadge({ status, dict }: { status: string; dict: Dict }) {
  const labels: Record<string, string> = dict.dashboard.status;
  const key = status.toLowerCase();
  const label = labels[key] ?? status;
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium text-sidebar-foreground ${ORDER_BADGE[status] ?? "bg-muted"}`}
    >
      <span aria-hidden="true" className={`size-1.5 rounded-full ${ORDER_DOT[status] ?? "bg-muted-foreground"}`} />
      {label}
    </span>
  );
}

function StatTile({
  stat,
  value,
  tint,
  icon,
}: {
  stat: { label: string; hint: string };
  value: number;
  tint: string;
  icon: ReactNode;
}) {
  return (
    <div className="rounded-lg border border-border bg-card p-6">
      <div className="flex items-center gap-3">
        <span aria-hidden="true" className={`flex size-10 items-center justify-center rounded-lg ${tint}`}>
          {icon}
        </span>
        <span className="text-sm font-medium">{stat.label}</span>
      </div>
      <p className="mt-3 text-xl font-semibold tracking-tight">{value}</p>
      <p className="mt-1 text-xs text-sidebar-foreground">{stat.hint}</p>
    </div>
  );
}

/** Streaming child: the session read sits before the try so the cacheComponents
 * prerender abort surfaces instead of being swallowed into the error UI (buyers pattern). */
async function DashboardData({ lang, dict }: { lang: Lang; dict: Dict }) {
  await getSessionToken();

  let buyers: BuyerDto[] = [];
  let styles: StyleDto[] = [];
  let orders: OrderDto[] = [];
  let failed = false;
  try {
    // pageSize=100 is the API cap; counts are exact for pilot-sized tenants.
    const [b, s, o] = await Promise.all([
      apiFetch<BuyerDto[]>("/ord/buyers?pageSize=100"),
      apiFetch<StyleDto[]>("/pdm/styles"),
      apiFetch<OrderDto[]>("/ord/customer-orders?pageSize=100"),
    ]);
    buyers = b.data;
    styles = s.data;
    orders = o.data;
  } catch {
    // Only real fetch failures land here: the prerender abort fired before the try.
    failed = true;
  }

  const d = dict.dashboard;

  if (failed) {
    return (
      <ErrorState
        title={d.states.error}
        body={d.states.errorBody}
        icon={<AlertIcon />}
        retryHref={`/${lang}/dashboard`}
        retryLabel={d.states.retry}
      />
    );
  }

  if (buyers.length === 0 && styles.length === 0 && orders.length === 0) {
    return (
      <div className="rounded-lg border border-border bg-card p-6">
        <EmptyState title={d.states.empty} body={d.states.emptyBody} icon={<InboxIcon />} />
      </div>
    );
  }

  const buyerById = new Map(buyers.map((b) => [b.id, b]));
  const locale = lang === "bn" ? "bn-BD" : "en-GB";
  const dateFmt = new Intl.DateTimeFormat(locale, {
    day: "numeric",
    month: "short",
    year: "numeric",
    timeZone: "UTC",
  });
  const amountFmt = (currency: string) =>
    new Intl.NumberFormat(locale, { style: "currency", currency, currencyDisplay: "code" });

  const tiles = [
    { stat: d.stats.buyers, value: buyers.length, tint: "bg-lightprimary text-primary", icon: <UsersIcon /> },
    { stat: d.stats.styles, value: styles.length, tint: "bg-lightinfo text-info", icon: <ShirtIcon /> },
    { stat: d.stats.orders, value: orders.length, tint: "bg-lightwarning text-warning", icon: <OrdersIcon /> },
  ];

  return (
    <div className="flex flex-col gap-6">
      <div className="grid grid-cols-1 gap-6 sm:grid-cols-3">
        {tiles.map((tile) => (
          <StatTile key={tile.stat.label} stat={tile.stat} value={tile.value} tint={tile.tint} icon={tile.icon} />
        ))}
      </div>
      <section aria-labelledby="recent-orders-title" className="rounded-lg border border-border bg-card p-6">
        <h2 id="recent-orders-title" className="text-base font-semibold">{d.recentOrders.title}</h2>
        <div className="mt-4 overflow-x-auto">
          {orders.length === 0 ? (
            <EmptyState title={d.recentOrders.empty} body={d.recentOrders.emptyBody} icon={<InboxIcon />} />
          ) : (
            <table className="w-full min-w-[560px] border-collapse text-sm">
              <thead>
                <tr className="border-b border-border text-start">
                  <th scope="col" className="px-2 py-2 text-start font-medium">{d.table.order}</th>
                  <th scope="col" className="px-2 py-2 text-start font-medium">{d.table.buyer}</th>
                  <th scope="col" className="px-2 py-2 text-start font-medium">{d.table.date}</th>
                  <th scope="col" className="px-2 py-2 text-start font-medium">{d.table.amount}</th>
                  <th scope="col" className="px-2 py-2 text-start font-medium">{d.table.status}</th>
                </tr>
              </thead>
              <tbody>
                {orders.slice(0, 5).map((order) => {
                  const buyer = buyerById.get(order.buyerId);
                  const amount = order.lines.reduce((sum, l) => sum + l.price * l.lineQty, 0);
                  return (
                    <tr key={order.id} className="border-b border-border last:border-b-0">
                      <td dir="auto" className="px-2 py-2.5 font-medium">{order.code}</td>
                      <td dir="auto" className="px-2 py-2.5 text-sidebar-foreground">
                        {buyer ? buyer.name : "—"}
                      </td>
                      <td className="px-2 py-2.5 text-sidebar-foreground">{dateFmt.format(new Date(order.orderDate))}</td>
                      <td className="px-2 py-2.5 text-sidebar-foreground">{amountFmt(order.currency).format(amount)}</td>
                      <td className="px-2 py-2.5">
                        <OrderStatusBadge status={order.status} dict={dict} />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      </section>
    </div>
  );
}

export default async function DashboardPage({ params }: { params: Promise<{ lang: Lang }> }) {
  const { lang } = await params;
  const dict = getDict(lang);
  const d = dict.dashboard;

  return (
    <div className="flex flex-col gap-6">
      <header>
        <h1 className="text-xl font-semibold tracking-tight">{d.title}</h1>
        <p className="mt-1 text-sm text-sidebar-foreground">{d.subtitle}</p>
      </header>
      <Suspense fallback={<DashboardSkeleton label={d.states.loading} />}>
        <DashboardData lang={lang} dict={dict} />
      </Suspense>
    </div>
  );
}
