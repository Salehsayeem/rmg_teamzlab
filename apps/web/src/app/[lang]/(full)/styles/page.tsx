import { Suspense } from "react";
import Link from "next/link";
import { notFound } from "next/navigation";
import { apiFetch } from "@/lib/api";
import { StatusBadge } from "@/components/styles/StatusBadge";
import { InboxIcon, AlertIcon } from "@/components/icons";
import { getDict, isLang, type Dict, type Lang } from "@/lib/i18n/getDict";
import type { StyleDto } from "@/lib/pdm";

/**
 * Styles list (Task 20): GET /pdm/styles inside Suspense gives the four states
 * — skeleton while streaming, empty, error-retry, and the table on success.
 * Buyer names are looked up from the ord.buyers list (non-fatal if it fails).
 */

const controlLink =
  "inline-flex h-10 items-center rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";


function StylesSkeleton({ label }: { label: string }) {
  return (
    <div role="status" aria-label={label} className="flex flex-col gap-2">
      {[0, 1, 2, 3].map((i) => (
        <div key={i} className="flex h-10 items-center gap-4">
          <span className="h-4 w-20 animate-pulse rounded bg-muted" />
          <span className="h-4 w-48 animate-pulse rounded bg-muted" />
          <span className="ms-auto h-4 w-16 animate-pulse rounded bg-muted" />
        </div>
      ))}
    </div>
  );
}

function EmptyState({ title, body, icon }: { title: string; body: string; icon: React.ReactNode }) {
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

function ErrorState({ lang, title, body, retry }: { lang: Lang; title: string; body: string; retry: string }) {
  return (
    <div role="alert" className="flex flex-col items-center gap-3 py-10 text-center">
      <span aria-hidden="true" className="flex size-12 items-center justify-center rounded-full bg-lighterror text-error">
        <AlertIcon />
      </span>
      <p className="text-sm font-medium">{title}</p>
      <p className="max-w-sm text-sm text-sidebar-foreground">{body}</p>
      <Link href={`/${lang}/styles`} replace className="inline-flex h-10 items-center rounded-lg border border-input px-4 text-sm font-medium transition-opacity hover:opacity-80 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">
        {retry}
      </Link>
    </div>
  );
}

async function StylesTable({ lang, dict }: { lang: Lang; dict: Dict }) {
  let styles: StyleDto[];
  let buyers: Map<string, string>;
  try {
    ({ data: styles } = await apiFetch<StyleDto[]>("/pdm/styles"));
  } catch {
    const s = dict.styles.states;
    return <ErrorState lang={lang} title={s.error} body={s.errorBody} retry={s.retry} />;
  }
  // Buyer names are a nicety — degrade to buyer id when the buyers read fails.
  try {
    const { data: buyerRows } = await apiFetch<{ id: string; code: string; name: string }[]>("/ord/buyers?pageSize=100");
    buyers = new Map(buyerRows.map((b) => [b.id, `${b.code} — ${b.name}`]));
  } catch {
    buyers = new Map();
  }
  const s = dict.styles.states;
  if (styles.length === 0) return <EmptyState title={s.empty} body={s.emptyBody} icon={<InboxIcon />} />;
  return (
    <table className="w-full border-collapse text-sm">
      <thead>
        <tr className="border-b border-border">
          {(Object.keys(dict.styles.table) as (keyof Dict["styles"]["table"])[]).map((key) => (
            <th key={key} scope="col" className="px-2 py-2 text-start font-medium">{dict.styles.table[key]}</th>
          ))}
        </tr>
      </thead>
      <tbody>
        {styles.map((style) => (
          <tr key={style.id} className="border-b border-border last:border-b-0">
            <td className="px-2 py-2.5 font-medium">
              <Link href={`/${lang}/styles/${style.id}`} className="transition-colors hover:text-primary">
                {style.code}
              </Link>
            </td>
            <td dir="auto" className="px-2 py-2.5">{style.name}</td>
            <td className="px-2 py-2.5 text-sidebar-foreground">{style.season ?? "—"}</td>
            <td className="px-2 py-2.5 text-sidebar-foreground">{style.category ?? "—"}</td>
            <td dir="auto" className="px-2 py-2.5 text-sidebar-foreground">{buyers.get(style.buyerId) ?? "—"}</td>
            <td className="px-2 py-2.5 text-sidebar-foreground">{style.versions[0]?.variants.length ?? 0}</td>
            <td className="px-2 py-2.5">
              <StatusBadge status={style.status} labels={dict.styles.status} />
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

export default async function StylesPage({ params }: { params: Promise<{ lang: Lang }> }) {
  const { lang } = await params;
  if (!isLang(lang)) notFound();
  const dict = getDict(lang);
  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">{dict.styles.title}</h1>
          <p className="mt-1 text-sm text-sidebar-foreground">{dict.styles.subtitle}</p>
        </div>
        <Link href={`/${lang}/styles/new`} className={controlLink}>
          {dict.styles.newStyle}
        </Link>
      </header>
      <section aria-labelledby="styles-table-title" className="rounded-lg border border-border bg-card p-6">
        <h2 id="styles-table-title" className="sr-only">{dict.styles.title}</h2>
        <Suspense fallback={<StylesSkeleton label={dict.styles.states.loading} />}>
          <StylesTable lang={lang} dict={dict} />
        </Suspense>
      </section>
    </div>
  );
}
