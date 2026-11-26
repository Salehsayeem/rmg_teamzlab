import { Suspense } from "react";
import Link from "next/link";
import { AlertIcon, ChevronLeftIcon, ChevronRightIcon, InboxIcon, PlusIcon } from "@/components/icons";
import {
  BuyersSkeleton,
  DEFAULT_PAGE_SIZE,
  EmptyState,
  ErrorState,
  NO_CURSOR,
  StatusBadge,
  buyersHref,
  type BuyerDto,
} from "@/components/buyers/BuyerStates";
import { apiFetch } from "@/lib/api";
import { can } from "@/lib/perm";
import { getDict, type Dict, type Lang } from "@/lib/i18n/getDict";

type SearchParams = { cursor?: string; hist?: string; pageSize?: string };

/** Streaming child: every runtime read (cookies, searchParams, fetch) sits inside the Suspense boundary. */
async function BuyersList({ sp, lang, dict }: { sp: Promise<SearchParams>; lang: Lang; dict: Dict }) {
  const search = await sp;
  const pageSize = Math.min(Math.max(Number(search.pageSize) || DEFAULT_PAGE_SIZE, 1), 100);
  const hist = search.hist ? search.hist.split(",") : [];
  const cursor = search.cursor;
  const d = dict.buyers;
  const canCreate = await can("ord.buyer.create");

  let rows: BuyerDto[] = [];
  let nextCursor: string | undefined;
  let failed = false;
  try {
    const q = new URLSearchParams({ pageSize: String(pageSize) });
    if (cursor) q.set("cursor", cursor);
    const { data, meta } = await apiFetch<BuyerDto[]>(`/ord/buyers?${q}`);
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

  const retryHref = buyersHref(lang, pageSize, cursor, search.hist);

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

  const newBuyerButton = canCreate && (
    <Link
      href={`/${lang}/buyers/new`}
      className="flex h-10 items-center gap-2 rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
    >
      <PlusIcon />
      {d.newBuyer}
    </Link>
  );

  if (rows.length === 0 && !cursor) {
    return (
      <div className="flex flex-col gap-4">
        {newBuyerButton && <div className="flex justify-end">{newBuyerButton}</div>}
        <div className="rounded-lg border border-border bg-card p-6">
          <EmptyState title={d.states.empty} body={d.states.emptyBody} icon={<InboxIcon />} />
        </div>
      </div>
    );
  }

  const nextHref = nextCursor
    ? buyersHref(lang, pageSize, nextCursor, [...hist, cursor ?? NO_CURSOR].join(","))
    : null;
  const prevMarker = hist[hist.length - 1];
  const prevHref =
    hist.length > 0
      ? buyersHref(lang, pageSize, prevMarker === NO_CURSOR ? undefined : prevMarker, hist.slice(0, -1).join(","))
      : null;

  const pageBtn =
    "flex h-10 items-center gap-1.5 rounded-lg border border-border px-3 text-sm font-medium text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";
  const pageBtnOff = "flex h-10 items-center gap-1.5 rounded-lg border border-border px-3 text-sm font-medium text-sidebar-foreground opacity-50";

  return (
    <div className="flex flex-col gap-4">
      {newBuyerButton && <div className="flex justify-end">{newBuyerButton}</div>}
      <section aria-labelledby="buyers-table-title" className="rounded-lg border border-border bg-card p-6">
        <h2 id="buyers-table-title" className="text-base font-semibold">
          {d.title}
        </h2>
        <div className="mt-4 overflow-x-auto">
          <table className="w-full min-w-[560px] border-collapse text-sm">
            <thead>
              <tr className="border-b border-border text-start">
                <th scope="col" className="px-2 py-2 text-start font-medium">{d.table.code}</th>
                <th scope="col" className="px-2 py-2 text-start font-medium">{d.table.name}</th>
                <th scope="col" className="px-2 py-2 text-start font-medium">{d.table.country}</th>
                <th scope="col" className="px-2 py-2 text-start font-medium">{d.table.currency}</th>
                <th scope="col" className="px-2 py-2 text-start font-medium">{d.table.status}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.id} className="border-b border-border last:border-b-0">
                  <td className="px-2 py-2.5 font-medium">
                    <Link href={`/${lang}/buyers/${row.id}`} className="hover:text-primary hover:underline">
                      {row.code}
                    </Link>
                  </td>
                  <td dir="auto" className="px-2 py-2.5 text-sidebar-foreground">{row.name}</td>
                  <td dir="auto" className="px-2 py-2.5 text-sidebar-foreground">{row.country}</td>
                  <td className="px-2 py-2.5 text-sidebar-foreground">{row.currency}</td>
                  <td className="px-2 py-2.5">
                    <StatusBadge status={row.status} dict={dict} />
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

export default async function BuyersPage({
  params,
  searchParams,
}: {
  params: Promise<{ lang: Lang }>;
  searchParams: Promise<SearchParams>;
}) {
  const { lang } = await params;
  const dict = getDict(lang);
  const d = dict.buyers;

  return (
    <div className="flex flex-col gap-6">
      <header>
        <h1 className="text-xl font-semibold tracking-tight">{d.title}</h1>
        <p className="mt-1 text-sm text-sidebar-foreground">{d.subtitle}</p>
      </header>
      <Suspense fallback={<BuyersSkeleton label={d.states.loading} />}>
        <BuyersList sp={searchParams} lang={lang} dict={dict} />
      </Suspense>
    </div>
  );
}
