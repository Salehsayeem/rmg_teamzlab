import { Suspense } from "react";
import { notFound } from "next/navigation";
import { AlertIcon } from "@/components/icons";
import {
  BomErrorState,
  BomSkeleton,
  BomStatusBadge,
  type BomDto,
  type MaterialDto,
  type StyleDto,
  type UomDto,
} from "@/components/boms/BomStates";
import { AddLineForm, CreateBomPanel, ReleasePanel } from "@/components/boms/BomForms";
import { ApiError, apiFetch } from "@/lib/api";
import { can } from "@/lib/perm";
import { getDict, type Lang } from "@/lib/i18n/getDict";

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// A localized static shell is impossible here: the skeleton label needs `lang`,
// but the [id] segment is not enumerable, so awaiting params outside Suspense is
// itself the runtime access. instant=false opts out of shell prerendering —
// the route renders blocking instead (documented escape hatch, T19 precedent).
export const instant = false;

/** Streaming child: every runtime read sits inside the Suspense boundary. */
async function BomScreen({ params }: { params: Promise<{ lang: string; id: string }> }) {
  // The `id` read is the runtime access — it must stay inside this boundary
  // (`lang` alone is enumerable at build; `id` only exists at request time).
  const { lang: langName, id } = await params;
  if (!GUID.test(id)) notFound();
  const lang = langName as Lang;
  const dict = getDict(lang);
  const d = dict.boms;

  // Perms decode the session JWT payload (unverified; API re-checks every request).
  const canCreate = await can("pdm.bom.create");
  const canEdit = await can("pdm.bom.edit");
  const canApprove = await can("pdm.bom.approve");

  let style: StyleDto | undefined;
  let bom: BomDto | undefined;
  let materials: MaterialDto[] = [];
  let uoms: UomDto[] = [];
  let failed = false;
  try {
    // The style gates everything else (404 -> not-found UI).
    style = (await apiFetch<StyleDto>(`/pdm/styles/${id}`)).data;
    const [bomsRes, materialsRes, uomsRes] = await Promise.all([
      apiFetch<BomDto[]>("/pdm/boms"),
      apiFetch<MaterialDto[]>("/pdm/materials"),
      apiFetch<UomDto[]>("/pdm/uoms"),
    ]);
    // The latest version owns the BOM (one BOM per style version).
    const version = [...style.versions].sort((a, b) => b.version - a.version)[0];
    bom = version ? bomsRes.data.find((b) => b.styleVersionId === version.id) : undefined;
    materials = materialsRes.data;
    uoms = uomsRes.data;
  } catch (e) {
    // The prerender abort cannot land here: params and cookies (can()) are read
    // before the try. 404 -> not-found UI; other failures -> error state.
    if (e instanceof ApiError && e.status === 404) notFound();
    failed = true;
  }

  if (failed || !style) {
    return (
      <BomErrorState
        title={d.states.error}
        body={d.states.errorBody}
        icon={<AlertIcon />}
        retryHref={`/${lang}/styles/${id}/bom`}
        retryLabel={d.states.retry}
      />
    );
  }

  const version = [...style.versions].sort((a, b) => b.version - a.version)[0];

  const isDraft = bom?.status === "Draft";
  const isReleased = bom?.status === "Released";

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">{style.name}</h1>
          <p className="mt-1 text-sm text-sidebar-foreground">
            {style.code} · {d.versionLabel} {version ? version.version : "—"}
          </p>
        </div>
        {bom && <BomStatusBadge status={bom.status} dict={dict} />}
      </header>

      {bom && (
        <section aria-labelledby="bom-lines-title" className="rounded-lg border border-border bg-card p-6">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <h2 id="bom-lines-title" className="text-base font-semibold">{d.linesTitle}</h2>
          </div>
          <div className="mt-4">
            {bom.lines.length === 0 ? (
              <p className="py-6 text-center text-sm text-sidebar-foreground">{d.linesEmpty}</p>
            ) : (
              <table className="w-full border-collapse text-sm">
                <thead>
                  <tr className="border-b border-border">
                    <th scope="col" className="px-2 py-2 text-start text-sm font-medium">{d.table.material}</th>
                    <th scope="col" className="px-2 py-2 text-start text-sm font-medium">{d.table.qty}</th>
                    <th scope="col" className="px-2 py-2 text-start text-sm font-medium">{d.table.uom}</th>
                    <th scope="col" className="px-2 py-2 text-start text-sm font-medium">{d.table.waste}</th>
                  </tr>
                </thead>
                <tbody>
                  {bom.lines.map((line) => {
                    const material = materials.find((m) => m.id === line.materialId);
                    return (
                      <tr key={line.id} className="border-b border-border last:border-b-0">
                        <td dir="auto" className="px-2 py-2.5 font-medium">{material ? material.name : line.materialId}</td>
                        <td className="px-2 py-2.5 text-sidebar-foreground">{line.qtyPerUnit}</td>
                        <td className="px-2 py-2.5 text-sidebar-foreground">{line.uomCode}</td>
                        <td className="px-2 py-2.5 text-sidebar-foreground">{line.wastePct}%</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
          {isReleased && <p className="mt-4 text-xs text-sidebar-foreground">{d.release.confirmBody}</p>}
        </section>
      )}
      {bom && isDraft && (
        <AddLineForm
          lang={lang}
          dict={dict}
          styleId={id}
          bomId={bom.id}
          materials={materials}
          uoms={uoms}
          canEdit={canEdit}
        />
      )}

      {bom && isDraft && (
        <ReleasePanel lang={lang} dict={dict} styleId={id} bomId={bom.id} canApprove={canApprove} />
      )}

      {!bom && version && (
        <CreateBomPanel lang={lang} dict={dict} styleId={id} styleVersionId={version.id} canCreate={canCreate} />
      )}
      {!bom && !version && <p className="text-sm text-sidebar-foreground">{d.create.whenNone}</p>}
    </div>
  );
}

export default async function BomPage({ params }: { params: Promise<{ lang: string; id: string }> }) {
  const { lang } = await params;
  return (
    <Suspense fallback={<BomSkeleton label={getDict(lang as Lang).boms.states.loading} />}>
      <BomScreen params={params} />
    </Suspense>
  );
}
