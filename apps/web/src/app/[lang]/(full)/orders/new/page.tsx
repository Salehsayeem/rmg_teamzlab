import { Suspense } from "react";
import Link from "next/link";
import { notFound } from "next/navigation";
import { AlertIcon, ChevronLeftIcon } from "@/components/icons";
import { OrdersSkeleton, ErrorState } from "@/components/orders/OrderStates";
import { OrderForm, type BuyerOption, type VersionOption } from "@/components/orders/OrderForm";
import { apiFetch } from "@/lib/api";import { can } from "@/lib/perm";
import { getDict, isLang, type Dict, type Lang } from "@/lib/i18n/getDict";
import type { BuyerDto } from "@/components/buyers/BuyerStates";

/** pdm.style read shape — only the fields order capture consumes (structural; full type lives with the styles screens). */
type StyleRow = {
  id: string;
  code: string;
  versions: { id: string; version: number; status: string; variants: { colorCode: string; sizeCode: string }[] }[];
};

/** Streaming child: the runtime reads (cookies via apiFetch) sit inside the Suspense boundary. */
async function NewOrderForm({ lang, dict }: { lang: Lang; dict: Dict }) {
  const t = dict.orders;
  const canCreate = await can("ord.order.create");

  let buyers: BuyerDto[] = [];
  let styles: StyleRow[] = [];
  let failed = false;
  try {
    // Reference data for the form: buyers (ord.buyer.read) + styles (pdm.style.read).
    // Reads happen before the try's failure flag — a perm gap surfaces as the error state.
    buyers = (await apiFetch<BuyerDto[]>("/ord/buyers?pageSize=100")).data;
    styles = (await apiFetch<StyleRow[]>("/pdm/styles")).data;
  } catch {
    failed = true;
  }

  if (!canCreate) {
    return (
      <ErrorState
        title={t.states.error}
        body={t.errors.generic}
        icon={<AlertIcon />}
        retryHref={`/${lang}/orders`}
        retryLabel={t.states.retry}
      />
    );
  }
  if (failed) {
    return (
      <ErrorState
        title={t.states.error}
        body={t.states.errorBody}
        icon={<AlertIcon />}
        retryHref={`/${lang}/orders`}
        retryLabel={t.states.retry}
      />
    );
  }

  // Approved versions only, grouped by style code: the capture form's options.
  const buyersOptions: BuyerOption[] = buyers.map((buyer) => ({
    id: buyer.id,
    label: `${buyer.code} — ${buyer.name}`,
    currency: buyer.currency,
  }));
  const versionOptions: VersionOption[] = styles
    .flatMap((style) =>
      style.versions
        .filter((version) => version.status === "Approved")
        .map((version) => {
          const colors = [...new Set(version.variants.map((variant) => variant.colorCode))];
          const sizes = [...new Set(version.variants.map((variant) => variant.sizeCode))];
          return {
            id: version.id,
            label: `${style.code} · v${version.version}`,
            colors,
            sizes,
            cells: version.variants.map((variant) => `${variant.colorCode}|${variant.sizeCode}`),
          };
        }),
    )
    .sort((a, b) => a.label.localeCompare(b.label));

  return (
    <OrderForm
      lang={lang}
      dict={dict}
      buyers={buyersOptions}
      versions={versionOptions}
      defaultDate={new Date().toISOString().slice(0, 10)}
    />
  );
}

export default async function NewOrderPage({ params }: { params: Promise<{ lang: string }> }) {
  const { lang } = await params;
  if (!isLang(lang)) notFound();
  const dict = getDict(lang);
  const t = dict.orders;

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
      <header>
        <h1 className="text-xl font-semibold tracking-tight">{t.form.title}</h1>
        <p className="mt-1 text-sm text-sidebar-foreground">{t.form.subtitle}</p>
      </header>
      <Suspense fallback={<OrdersSkeleton label={t.states.loading} />}>
        <NewOrderForm lang={lang} dict={dict} />
      </Suspense>
    </div>
  );
}
