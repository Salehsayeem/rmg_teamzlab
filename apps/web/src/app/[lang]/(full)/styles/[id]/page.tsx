import { notFound } from "next/navigation";
import Link from "next/link";
import { apiFetch, ApiError } from "@/lib/api";
import { getPerms } from "@/lib/auth";
import { StatusBadge } from "@/components/styles/StatusBadge";
import { ApproveButton } from "@/components/styles/ApproveButton";
import { AlertIcon } from "@/components/icons";
import { getDict, isLang, type Lang } from "@/lib/i18n/getDict";
import type { BomDto, StyleDto } from "@/lib/pdm";

/**
 * Style detail (Task 20): current version + variant matrix + linked BOM status.
 * Reads happen before the try blocks so a plain catch can never swallow the
 * prerender abort (T19 learning). Unknown ids 404; BOM status is a nicety and
 * degrades to "none" when the boms read fails.
 */
// Blocking route: the session-cookie reads cannot sit behind Suspense here
// (documented escape hatch, cf. the buyers screens).
export const instant = false;

export default async function StyleDetailPage({ params }: { params: Promise<{ lang: Lang; id: string }> }) {
  const { lang, id } = await params;
  if (!isLang(lang)) notFound();
  const dict = getDict(lang);
  const d = dict.styles;
  const perms = await getPerms();

  let style: StyleDto;
  try {
    ({ data: style } = await apiFetch<StyleDto>(`/pdm/styles/${id}`));
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) notFound();
    const s = d.states;
    return (
      <div role="alert" className="flex flex-col items-center gap-3 py-10 text-center">
        <span aria-hidden="true" className="flex size-12 items-center justify-center rounded-full bg-lighterror text-error">
          <AlertIcon />
        </span>
        <p className="text-sm font-medium">{s.error}</p>
        <p className="max-w-sm text-sm text-sidebar-foreground">{s.errorBody}</p>
        <Link href={`/${lang}/styles`} replace className="inline-flex h-10 items-center rounded-lg border border-input px-4 text-sm font-medium transition-opacity hover:opacity-80 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">
          {s.retry}
        </Link>
      </div>
    );
  }

  const version = style.versions[0];
  let bom: BomDto | null = null;
  try {
    const { data: boms } = await apiFetch<BomDto[]>("/pdm/boms");
    bom = boms.find((b) => b.styleVersionId === version?.id) ?? null;
  } catch {
    // BOM status stays null -> rendered as "no BOM for this version yet".
  }

  let buyer: string | null = null;
  try {
    const { data: buyers } = await apiFetch<{ id: string; code: string; name: string }[]>("/ord/buyers?pageSize=100");
    const row = buyers.find((b) => b.id === style.buyerId);
    buyer = row ? `${row.code} — ${row.name}` : null;
  } catch {
    // Buyer display name is a nicety; fall back to "—".
  }

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <Link href={`/${lang}/styles`} className="text-sm text-sidebar-foreground transition-colors hover:text-foreground">
            {d.backToList}
          </Link>
          <h1 className="mt-1 text-xl font-semibold tracking-tight" dir="auto">
            {style.code} — {style.name}
          </h1>
          <div className="mt-2 flex flex-wrap items-center gap-3">
            <StatusBadge status={style.status} labels={d.status} />
            <span className="text-xs text-sidebar-foreground">
              {d.detail.revision}: {style.revision}
            </span>
          </div>
        </div>
        {style.status === "Draft" && <ApproveButton id={style.id} dict={d} canApprove={perms.includes("pdm.style.approve")} />}
      </header>
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <section aria-labelledby="style-facts-title" className="rounded-lg border border-border bg-card p-6 lg:col-span-1">
          <h2 id="style-facts-title" className="text-base font-semibold">{d.detail.facts}</h2>
          <dl className="mt-4 flex flex-col gap-3 text-sm">
            <div className="flex justify-between gap-3">
              <dt className="text-sidebar-foreground">{d.detail.buyer}</dt>
              <dd dir="auto" className="text-end font-medium">{buyer ?? "—"}</dd>
            </div>
            <div className="flex justify-between gap-3">
              <dt className="text-sidebar-foreground">{d.detail.season}</dt>
              <dd className="font-medium">{style.season ?? "—"}</dd>
            </div>
            <div className="flex justify-between gap-3">
              <dt className="text-sidebar-foreground">{d.detail.category}</dt>
              <dd className="font-medium">{style.category ?? "—"}</dd>
            </div>
            <div className="flex justify-between gap-3">
              <dt className="text-sidebar-foreground">{d.detail.version}</dt>
              <dd className="font-medium">{version ? String(version.version) : "—"}</dd>
            </div>
            {version?.approvedAtUtc && (
              <div className="flex justify-between gap-3">
                <dt className="text-sidebar-foreground">{d.detail.approvedAt}</dt>
                <dd className="font-medium">{new Date(version.approvedAtUtc).toLocaleString(lang === "bn" ? "bn-BD" : "en-GB")}</dd>
              </div>
            )}
          </dl>
        </section>
        <section aria-labelledby="style-variants-title" className="rounded-lg border border-border bg-card p-6 lg:col-span-2">
          <h2 id="style-variants-title" className="text-base font-semibold">{d.detail.variants}</h2>
          {version && version.variants.length > 0 ? (
            <div className="mt-4 overflow-x-auto">
              <table className="w-full border-collapse text-sm">
                <thead>
                  <tr className="border-b border-border">
                    <th scope="col" className="px-2 py-2 text-start font-medium">{d.detail.variantsTable.color}</th>
                    <th scope="col" className="px-2 py-2 text-start font-medium">{d.detail.variantsTable.size}</th>
                  </tr>
                </thead>
                <tbody>
                  {version.variants.map((v) => (
                    <tr key={v.id} className="border-b border-border last:border-b-0">
                      <td dir="auto" className="px-2 py-2.5 font-medium">{v.colorCode}</td>
                      <td dir="auto" className="px-2 py-2.5 text-sidebar-foreground">{v.sizeCode}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="mt-4 text-sm text-sidebar-foreground">{d.detail.variantsEmpty}</p>
          )}
        </section>
      </div>
      <section aria-labelledby="style-bom-title" className="rounded-lg border border-border bg-card p-6">
        <h2 id="style-bom-title" className="text-base font-semibold">{d.detail.bom}</h2>
        <div className="mt-3 flex items-center gap-3 text-sm">
          {bom ? (
            <span className="inline-flex items-center gap-2">
              <span className="rounded-lg bg-lightprimary px-2.5 py-1 font-mono text-xs text-primary">{bom.id.slice(0, 8)}</span>
              <StatusBadge status={bom.status} labels={d.status} />
            </span>
          ) : (
            <p className="text-sidebar-foreground">{d.detail.bomNone}</p>
          )}
        </div>
      </section>
    </div>
  );
}
