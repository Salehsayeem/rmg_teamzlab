import { Suspense } from "react";
import Link from "next/link";
import { notFound } from "next/navigation";
import { AlertIcon, ChevronLeftIcon } from "@/components/icons";
import { OrderActions } from "@/components/orders/OrderActions";
import { ErrorState, OrderStatusBadge, OrdersSkeleton, type OrderDto } from "@/components/orders/OrderStates";
import { ApiError, apiFetch } from "@/lib/api";
import { can } from "@/lib/perm";
import { getDict, type Lang } from "@/lib/i18n/getDict";

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// A localized static shell is impossible here: the skeleton label needs `lang`,
// but the [id] segment is not enumerable, so awaiting params outside Suspense is
// itself the runtime access. instant=false opts out of shell prerendering —
// the route renders blocking instead (documented escape hatch, buyers pattern).
export const instant = false;

type VersionLabels = Record<string, string>;

/** pdm.style read shape — only the fields the detail labels consume (structural). */
type StyleRow = {
  id: string;
  code: string;
  versions: { id: string; version: number; status: string; variants: { colorCode: string; sizeCode: string }[] }[];
};

/** Streaming child: fetch + perm reads sit inside the Suspense boundary (cacheComponents). */
async function OrderDetail({ params }: { params: Promise<{ lang: string; id: string }> }) {
  // The `id` read is the runtime access — it must stay inside this boundary
  // (`lang` alone is enumerable at build; `id` only exists at request time).
  const { lang: langName, id } = await params;
  if (!GUID.test(id)) notFound();
  const lang = langName as Lang;
  const dict = getDict(lang);
  const t = dict.orders;

  const canConfirm = await can("ord.order.confirm");
  const canEdit = await can("ord.order.edit");

  let order: OrderDto | undefined;
  let failed = false;
  try {
    order = (await apiFetch<OrderDto>(`/ord/customer-orders/${id}`)).data;
  } catch (e) {
    // 404 -> the not-found UI; any other failure (network, 5xx) -> the error state.
    // The prerender abort cannot land here: params and cookies (can()) are read before the try.
    if (e instanceof ApiError && e.status === 404) notFound();
    failed = true;
  }

  if (failed || !order) {
    return (
      <ErrorState
        title={t.states.error}
        body={t.states.errorBody}
        icon={<AlertIcon />}
        retryHref={`/${lang}/orders/${id}`}
        retryLabel={t.states.retry}
      />
    );
  }

  // Display labels only — failures degrade to the raw ids (a user with ord.order.read
  // but no ord.buyer.read / pdm.style.read still gets the full order).
  let buyerLabel = order.buyerId;
  try {
    const buyer = (await apiFetch<{ code: string; name: string }>(`/ord/buyers/${order.buyerId}`)).data;
    buyerLabel = `${buyer.code} — ${buyer.name}`;
  } catch {
    // keep the raw buyer id
  }
  const versionLabels: VersionLabels = {};
  try {
    const styles = (await apiFetch<StyleRow[]>("/pdm/styles")).data;
    for (const style of styles) {
      for (const version of style.versions) {
        versionLabels[version.id] = `${style.code} · v${version.version}`;
      }
    }
  } catch {
    // keep the raw version ids
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <Link
          href={`/${lang}/orders`}
          className="inline-flex h-10 items-center gap-1.5 text-sm font-medium text-sidebar-foreground transition-colors hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          <ChevronLeftIcon />
          {t.detail.back}
        </Link>
      </div>
      <header className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">{order.code}</h1>
          <p dir="auto" className="mt-1 text-sm text-sidebar-foreground">{order.buyerPo}</p>
        </div>
        <OrderStatusBadge status={order.status} dict={dict} />
      </header>
      <section className="rounded-lg border border-border bg-card p-6">
        <dl className="grid grid-cols-1 gap-x-8 gap-y-4 sm:grid-cols-2 xl:grid-cols-3">
          <div>
            <dt className="text-sm text-sidebar-foreground">{t.detail.fields.code}</dt>
            <dd dir="auto" className="mt-0.5 font-medium">{order.code}</dd>
          </div>
          <div>
            <dt className="text-sm text-sidebar-foreground">{t.detail.fields.buyer}</dt>
            <dd dir="auto" className="mt-0.5 font-medium">{buyerLabel}</dd>
          </div>
          <div>
            <dt className="text-sm text-sidebar-foreground">{t.detail.fields.buyerPo}</dt>
            <dd dir="auto" className="mt-0.5 font-medium">{order.buyerPo}</dd>
          </div>
          <div>
            <dt className="text-sm text-sidebar-foreground">{t.detail.fields.currency}</dt>
            <dd className="mt-0.5 font-medium">{order.currency}</dd>
          </div>
          <div>
            <dt className="text-sm text-sidebar-foreground">{t.detail.fields.orderDate}</dt>
            <dd className="mt-0.5 font-medium">{order.orderDate}</dd>
          </div>
          <div>
            <dt className="text-sm text-sidebar-foreground">{t.detail.fields.status}</dt>
            <dd className="mt-0.5 font-medium">{order.status}</dd>
          </div>
          <div>
            <dt className="text-sm text-sidebar-foreground">{t.detail.fields.revision}</dt>
            <dd className="mt-0.5 font-medium">{order.revision}</dd>
          </div>
        </dl>
      </section>
      <section aria-labelledby="order-lines-title" className="rounded-lg border border-border bg-card p-6">
        <h2 id="order-lines-title" className="text-base font-semibold">{t.detail.lines}</h2>
        <div className="mt-4 flex flex-col gap-4">
          {order.lines.map((line) => (
            <div key={line.id} className="rounded-lg border border-border p-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <p className="text-sm font-medium">
                  {t.detail.line} {line.lineNo} — {versionLabels[line.styleVersionId] ?? line.styleVersionId}
                </p>
                <p className="text-sm text-sidebar-foreground">
                  {t.detail.price}: {line.price} · {t.detail.lineQty}: {line.lineQty}
                </p>
              </div>
              <div className="mt-3 overflow-x-auto">
                <table className="w-full min-w-[420px] border-collapse text-sm">
                  <thead>
                    <tr className="border-b border-border">
                      <th scope="col" className="px-2 py-2 text-start text-xs font-medium text-sidebar-foreground">
                        {t.detail.sizes}
                      </th>
                      <th scope="col" className="px-2 py-2 text-end text-xs font-medium text-sidebar-foreground">
                        {t.form.qty}
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {line.sizes.map((size) => (
                      <tr key={`${size.colorCode}|${size.sizeCode}`} className="border-b border-border last:border-b-0">
                        <td dir="auto" className="px-2 py-2 font-medium">
                          {size.colorCode} / {size.sizeCode}
                        </td>
                        <td className="px-2 py-2 text-end">{size.qty}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="mt-3 flex flex-col gap-1">
                <p className="text-sm font-medium">{t.detail.schedules}</p>
                {line.schedules.length === 0 ? (
                  <p className="text-xs text-sidebar-foreground">{t.detail.schedulesEmpty}</p>
                ) : (
                  <ul className="flex flex-col divide-y divide-border">
                    {line.schedules.map((schedule, i) => (
                      <li key={i} className="flex flex-wrap items-baseline gap-x-6 gap-y-1 py-2">
                        <span className="text-sm font-medium">{schedule.shipDate} — {schedule.qty}</span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          ))}
        </div>
      </section>
      <OrderActions
        lang={lang}
        dict={dict}
        orderId={order.id}
        status={order.status}
        revision={order.revision}
        canConfirm={canConfirm}
        canEdit={canEdit}
      />
    </div>
  );
}

export default async function OrderDetailPage({ params }: { params: Promise<{ lang: string; id: string }> }) {
  // Only `lang` is read here so the per-language static shell can prerender; the
  // [id] value is runtime data and is consumed inside the Suspense boundary below.
  const { lang } = await params;
  return (
    <Suspense fallback={<OrdersSkeleton label={getDict(lang as Lang).orders.states.loading} />}>
      <OrderDetail params={params} />
    </Suspense>
  );
}
