import { Suspense } from "react";
import Link from "next/link";
import { AlertIcon, ChevronLeftIcon, ChevronRightIcon, InboxIcon, PlusIcon } from "@/components/icons";
import {
  DEFAULT_PAGE_SIZE,
  EmptyState,
  ErrorState,
  NO_CURSOR,
  type OrderDto,
  OrderStatusBadge,
  OrdersSkeleton,
  ordersHref,
} from "@/components/orders/OrderStates";
import { apiFetch } from "@/lib/api";
import { can } from "@/lib/perm";
import { getDict, type Dict, type Lang } from "@/lib/i18n/getDict";

type SearchParams = { cursor?: string; hist?: string; pageSize?: string };

/** Streaming child: every runtime read (cookies, searchParams, fetch) sits inside the Suspense boundary. */
async function OrdersList({ sp, lang, dict }: { sp: Promise<SearchParams>; lang: Lang; dict: Dict }) {
  const search = await sp;
  const pageSize = Math.min(Math.max(Number(search.pageSize) || DEFAULT_PAGE_SIZE, 1), 100);
  const hist = search.hist ? search.hist.split(",") : [];
  const cursor = search.cursor;
  const canCreate = await can("ord.order.create");
  const d = dict.orders;

  let rows: OrderDto[] = [];
  let nextCursor: string | undefined;
  let failed = false;
  try {
    const q = new URLSearchParams({ pageSize: String(pageSize) });
    if (cursor) q.set("cursor", cursor);
    const { data, meta } = await apiFetch<OrderDto[]>(`/ord/customer-orders?${q}`);
    rows = data;
    // PageMeta is a non-dictionary, so Envelope.Of nests it under meta.meta (live wire format).
    const pageMeta = (meta.meta ?? meta) as { nextCursor?: unknown };
    nextCursor = typeof pageMeta.nextCursor === "string" ? pageMeta.nextCursor : undefined;
  } catch {
    // Network/API failures are the error UI state. The cacheComponents prerender
    // abort cannot land here: every runtime read (searchParams, cookies via can())
    // happens before the try, so inside the try only real fetch failures occur.
    failed = true;
  }

  const retryHref = ordersHref(lang, pageSize, cursor, search.hist);

  if (failed) {
    return (
      <ErrorState
        title={d.states.error}
        body={d.states.errorBody}
        icon={<AlertIcon />}
        retryHref={retryHref}
        retryLabel={d.states.retry}
      />
    );
  }

  const newOrderButton = canCreate && (
    <Link
      href={`/${lang}/orders/new`}
      className="flex h-10 items-center gap-2 rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
    >
      <PlusIcon />
      {d.newOrder}
    </Link>
  );

  if (rows.length === 0 && !cursor) {
    return (
      <div className="flex flex-col gap-4">
        {newOrderButton && <div className="flex justify-end">{newOrderButton}</div>}
        <div className="rounded-lg border border-border bg-card p-6">
          <EmptyState title={d.states.empty} body={d.states.emptyBody} icon={<InboxIcon />} />
        </div>
      </div>
    );
  }


  const nextHref = nextCursor
    ? ordersHref(lang, pageSize, nextCursor, [...hist, cursor ?? NO_CURSOR].join(","))
    : null;
  const prevMarker = hist[hist.length - 1];
  const prevHref =
    hist.length > 0
      ? ordersHref(lang, pageSize, prevMarker === NO_CURSOR ? undefined : prevMarker, hist.slice(0, -1).join(","))
      : null;

  const pageBtn =
    "flex h-10 items-center gap-1.5 rounded-lg border border-border px-3 text-sm font-medium text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";
  const pageBtnOff =
    "flex h-10 items-center gap-1.5 rounded-lg border border-border px-3 text-sm font-medium text-sidebar-foreground opacity-50";

  return (
    <div className="flex flex-col gap-4">
      {newOrderButton && <div className="flex justify-end">{newOrderButton}</div>}
      <section aria-labelledby="orders-table-title" className="rounded-lg border border-border bg-card p-6">
        <h2 id="orders-table-title" className="text-base font-semibold">
          {d.title}
        </h2>
        <div className="mt-4 overflow-x-auto">
          <table className="w-full min-w-[640px] border-collapse text-sm">
            <thead>
              <tr className="border-b border-border text-start">
                <th scope="col" className="px-2 py-2 text-start font-medium">{d.table.code}</th>
                <th scope="col" className="px-2 py-2 text-start font-medium">{d.table.buyerPo}</th>
                <th scope="col" className="px-2 py-2 text-start font-medium">{d.table.qty}</th>
                <th scope="col" className="px-2 py-2 text-start font-medium">{d.table.orderDate}</th>
                <th scope="col" className="px-2 py-2 text-start font-medium">{d.table.status}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.id} className="border-b border-border last:border-b-0">
                  <td className="px-2 py-2.5 font-medium">
                    <Link href={`/${lang}/orders/${row.id}`} className="hover:text-primary hover:underline">
                      {row.code}
                    </Link>
                  </td>
                  <td dir="auto" className="px-2 py-2.5 text-sidebar-foreground">{row.buyerPo}</td>
                  <td className="px-2 py-2.5 text-sidebar-foreground">
                    {row.lines.reduce((sum, line) => sum + line.lineQty, 0)}
                  </td>
                  <td className="px-2 py-2.5 text-sidebar-foreground">{row.orderDate}</td>
                  <td className="px-2 py-2.5">
                    <OrderStatusBadge status={row.status} dict={dict} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="mt-4 flex items-center justify-between">
          {prevHref ? (
            <Link href={prevHref} className={pageBtn}>
              <ChevronLeftIcon />
              {d.pagination.prev}
            </Link>
          ) : (
            <span aria-disabled="true" className={pageBtnOff}>
              <ChevronLeftIcon />
              {d.pagination.prev}
            </span>
          )}
          {nextHref ? (
            <Link href={nextHref} className={pageBtn}>
              {d.pagination.next}
              <ChevronRightIcon />
            </Link>
          ) : (
            <span aria-disabled="true" className={pageBtnOff}>
              {d.pagination.next}
              <ChevronRightIcon />
            </span>
          )}
        </div>
      </section>
    </div>
  );
}

export default async function OrdersPage({
  params,
  searchParams,
}: {
  params: Promise<{ lang: Lang }>;
  searchParams: Promise<SearchParams>;
}) {
  const { lang } = await params;
  const dict = getDict(lang);
  const d = dict.orders;

  return (
    <div className="flex flex-col gap-6">
      <header>
        <h1 className="text-xl font-semibold tracking-tight">{d.title}</h1>
        <p className="mt-1 text-sm text-sidebar-foreground">{d.subtitle}</p>
      </header>
      <Suspense fallback={<OrdersSkeleton label={d.states.loading} />}>
        <OrdersList sp={searchParams} lang={lang} dict={dict} />
      </Suspense>
    </div>
  );
}
