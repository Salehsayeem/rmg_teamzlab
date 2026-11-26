import { Suspense } from "react";
import Link from "next/link";
import { notFound } from "next/navigation";
import { AlertIcon, ChevronLeftIcon } from "@/components/icons";
import { BuyersSkeleton, ErrorState, StatusBadge, type BuyerDto } from "@/components/buyers/BuyerStates";
import { ApiError, apiFetch } from "@/lib/api";
import { can } from "@/lib/perm";
import { getDict, type Lang } from "@/lib/i18n/getDict";

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// A localized static shell is impossible here: the skeleton label needs `lang`,
// but the [id] segment is not enumerable, so awaiting params outside Suspense is
// itself the runtime access. instant=false opts out of shell prerendering —
// the route renders blocking instead (documented escape hatch).
export const instant = false;

/** Streaming child: fetch + perm read sit inside the Suspense boundary (cacheComponents). */
async function BuyerDetail({ params }: { params: Promise<{ lang: string; id: string }> }) {
  // The `id` read is the runtime access — it must stay inside this boundary
  // (`lang` alone is enumerable at build; `id` only exists at request time).
  const { lang: langName, id } = await params;
  if (!GUID.test(id)) notFound();
  const lang = langName as Lang;
  const dict = getDict(lang);
  const d = dict.buyers;

  const canManage = await can("ord.buyer.manage");

  let buyer: BuyerDto | undefined;
  let failed = false;
  try {
    buyer = (await apiFetch<BuyerDto>(`/ord/buyers/${id}`)).data;
  } catch (e) {
    // 404 -> the not-found UI; any other failure (network, 5xx) -> the error state.
    // The prerender abort cannot land here: params and cookies (can()) are read before the try.
    if (e instanceof ApiError && e.status === 404) notFound();
    failed = true;
  }

  if (failed || !buyer) {
    return (
      <ErrorState
        title={d.states.error}
        body={d.states.errorBody}
        icon={<AlertIcon />}
        retryHref={`/${lang}/buyers/${id}`}
        retryLabel={d.states.retry}
      />
    );
  }

  const hasDetails = buyer.contacts.some((c) => c.email || c.phone);

  return (
    <div className="flex flex-col gap-6">
      <div>
        <Link
          href={`/${lang}/buyers`}
          className="inline-flex h-10 items-center gap-1.5 text-sm font-medium text-sidebar-foreground transition-colors hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          <ChevronLeftIcon />
          {d.detail.back}
        </Link>
      </div>
      <header className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">{buyer.name}</h1>
          <p className="mt-1 text-sm text-sidebar-foreground">{buyer.code}</p>
        </div>
        <StatusBadge status={buyer.status} dict={dict} />
      </header>
      <section className="rounded-lg border border-border bg-card p-6">
        <dl className="grid grid-cols-1 gap-x-8 gap-y-4 sm:grid-cols-2 xl:grid-cols-3">
          <div>
            <dt className="text-sm text-sidebar-foreground">{d.detail.fields.code}</dt>
            <dd dir="auto" className="mt-0.5 font-medium">{buyer.code}</dd>
          </div>
          <div>
            <dt className="text-sm text-sidebar-foreground">{d.detail.fields.name}</dt>
            <dd dir="auto" className="mt-0.5 font-medium">{buyer.name}</dd>
          </div>
          <div>
            <dt className="text-sm text-sidebar-foreground">{d.detail.fields.country}</dt>
            <dd dir="auto" className="mt-0.5 font-medium">{buyer.country}</dd>
          </div>
          <div>
            <dt className="text-sm text-sidebar-foreground">{d.detail.fields.currency}</dt>
            <dd dir="auto" className="mt-0.5 font-medium">{buyer.currency}</dd>
          </div>
          <div>
            <dt className="text-sm text-sidebar-foreground">{d.detail.fields.status}</dt>
            <dd className="mt-0.5 font-medium">{buyer.status === "Active" ? d.status.active : buyer.status}</dd>
          </div>
          <div>
            <dt className="text-sm text-sidebar-foreground">{d.detail.fields.revision}</dt>
            <dd className="mt-0.5 font-medium">{buyer.revision}</dd>
          </div>
        </dl>
      </section>
      <section aria-labelledby="buyer-contacts-title" className="rounded-lg border border-border bg-card p-6">
        <h2 id="buyer-contacts-title" className="text-base font-semibold">{d.detail.contacts}</h2>
        {buyer.contacts.length === 0 ? (
          <p className="mt-3 text-sm text-sidebar-foreground">{d.detail.contactsEmpty}</p>
        ) : (
          <ul className="mt-2 flex flex-col divide-y divide-border">
            {buyer.contacts.map((c, i) => (
              <li key={i} className="flex flex-wrap items-baseline gap-x-6 gap-y-1 py-3">
                <span dir="auto" className="text-sm font-medium">{c.name}</span>
                {c.email && (
                  <span dir="auto" className="text-sm text-sidebar-foreground">{c.email}</span>
                )}
                {c.phone && (
                  <span dir="auto" className="text-sm text-sidebar-foreground">{c.phone}</span>
                )}
              </li>
            ))}
          </ul>
        )}
        {!hasDetails && buyer.contacts.length > 0 && !canManage && (
          <p className="mt-3 text-xs text-sidebar-foreground">{d.detail.contactsHint}</p>
        )}
      </section>
    </div>
  );
}

export default async function BuyerDetailPage({ params }: { params: Promise<{ lang: string; id: string }> }) {
  // Only `lang` is read here so the per-language static shell can prerender; the
  // [id] value is runtime data and is consumed inside the Suspense boundary below.
  const { lang } = await params;
  return (
    <Suspense fallback={<BuyersSkeleton label={getDict(lang as Lang).buyers.states.loading} />}>
      <BuyerDetail params={params} />
    </Suspense>
  );
}
