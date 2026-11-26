"use client";

import { useActionState, useState } from "react";
import Link from "next/link";
import { AlertIcon, PlusIcon } from "@/components/icons";
import { addLine, createBom, release, type BomActionState } from "@/lib/actions/boms";
import type { MaterialDto, UomDto } from "@/components/boms/BomStates";
import type { Dict, Lang } from "@/lib/i18n/getDict";

const CONTROL =
  "h-10 w-full rounded-lg border border-input bg-background px-3 text-sm text-foreground placeholder:text-sidebar-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring disabled:opacity-60";

const INITIAL: BomActionState = { status: "idle" };

/** Error banner with an optional reload-to-latest affordance (409 conflict). */
function ActionBanner({
  message,
  conflict,
  reloadHref,
  reloadLabel,
}: {
  message: string;
  conflict?: boolean;
  reloadHref?: string;
  reloadLabel?: string;
}) {
  return (
    <div role="alert" className="mt-4 flex gap-3 rounded-lg bg-lighterror p-4">
      <span aria-hidden="true" className="mt-0.5 text-error">
        <AlertIcon />
      </span>
      <div className="min-w-0 text-sm">
        <p className="font-medium">{message}</p>
        {conflict && reloadHref && (
          <Link href={reloadHref} className="mt-1 inline-block font-medium text-primary hover:underline">
            {reloadLabel}
          </Link>
        )}
      </div>
    </div>
  );
}

/**
 * Add-line form for a Draft BOM: material select, qty, UoM select and optional
 * waste %. Field-mapped business codes and VALIDATION_FAILED render inline with
 * aria-invalid wiring; the submit button (and controls) disable without
 * pdm.bom.edit, with a localized hint.
 */
export function AddLineForm({
  lang,
  dict,
  styleId,
  bomId,
  materials,
  uoms,
  canEdit,
}: {
  lang: Lang;
  dict: Dict;
  styleId: string;
  bomId: string;
  materials: MaterialDto[];
  uoms: UomDto[];
  canEdit: boolean;
}) {
  const t = dict.boms;
  const [state, formAction, pending] = useActionState(addLine.bind(null, lang, styleId, bomId), INITIAL);
  const fe = (field: string) => state.fieldErrors?.[field]?.[0];
  const describedBy = (field: string, id: string) => (fe(field) ? id : undefined);

  return (
    <section aria-labelledby="bom-add-line-title" className="rounded-lg border border-border bg-card p-6">
      <h2 id="bom-add-line-title" className="text-base font-semibold">{t.add.title}</h2>
      {state.status === "error" && state.message && (
        <ActionBanner message={state.message} conflict={state.conflict} reloadHref={`/${lang}/styles/${styleId}/bom`} reloadLabel={t.errors.reload} />
      )}
      <form action={formAction} className="mt-4 flex flex-col gap-4" noValidate>
        <div>
          <label htmlFor="bom-material" className="text-sm font-medium">{t.add.material}</label>
          <select
            id="bom-material"
            name="materialId"
            required
            disabled={!canEdit}
            defaultValue=""
            aria-invalid={fe("materialId") ? true : undefined}
            aria-describedby={describedBy("materialId", "bom-material-error")}
            className={`mt-1.5 ${CONTROL}`}
          >
            <option value="" disabled>
              {t.add.materialPlaceholder}
            </option>
            {materials.map((m) => (
              <option key={m.id} value={m.id}>
                {m.name} ({m.code})
              </option>
            ))}
          </select>
          {fe("materialId") && (
            <p id="bom-material-error" className="mt-1 text-xs text-error">{fe("materialId")}</p>
          )}
        </div>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          <div>
            <label htmlFor="bom-qty" className="text-sm font-medium">{t.add.qty}</label>
            <input
              id="bom-qty"
              name="qtyPerUnit"
              type="number"
              step="any"
              min="0"
              inputMode="decimal"
              required
              disabled={!canEdit}
              aria-invalid={fe("qtyPerUnit") ? true : undefined}
              aria-describedby={describedBy("qtyPerUnit", "bom-qty-error")}
              className={`mt-1.5 ${CONTROL}`}
            />
            {fe("qtyPerUnit") && (
              <p id="bom-qty-error" className="mt-1 text-xs text-error">{fe("qtyPerUnit")}</p>
            )}
          </div>
          <div>
            <label htmlFor="bom-uom" className="text-sm font-medium">{t.add.uom}</label>
            <select
              id="bom-uom"
              name="uomCode"
              required
              disabled={!canEdit}
              defaultValue=""
              aria-invalid={fe("uomCode") ? true : undefined}
              aria-describedby={describedBy("uomCode", "bom-uom-error")}
              className={`mt-1.5 ${CONTROL}`}
            >
              <option value="" disabled>
                {t.add.uomPlaceholder}
              </option>
              {uoms.map((u) => (
                <option key={u.code} value={u.code}>
                  {u.name} ({u.code})
                </option>
              ))}
            </select>
            {fe("uomCode") && (
              <p id="bom-uom-error" className="mt-1 text-xs text-error">
                {fe("uomCode")}
              </p>
            )}
          </div>
          <div>
            <label htmlFor="material-waste" className="text-sm font-medium">{t.add.waste}</label>
            <input
              id="material-waste"
              name="wastePct"
              type="number"
              step="any"
              min="0"
              inputMode="decimal"
              disabled={!canEdit}
              aria-invalid={fe("wastePct") ? true : undefined}
              aria-describedby={describedBy("wastePct", "material-waste-error")}
              className={`mt-1.5 ${CONTROL}`}
              placeholder="0"
            />
            {fe("wastePct") && (
              <p id="material-waste-error" className="mt-1 text-xs text-error">
                {fe("wastePct")}
              </p>
            )}
          </div>
        </div>
        <div className="mt-2 flex items-center justify-end gap-2">
          {!canEdit && <p className="me-auto text-xs text-sidebar-foreground">{t.perm.editHint}</p>}
          <button
            type="submit"
            disabled={pending || !canEdit}
            className="flex h-10 items-center gap-2 rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90 disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            <PlusIcon />
            {pending ? t.add.adding : t.add.add}
          </button>
        </div>
      </form>
    </section>
  );
}

/**
 * Release action with a styled inline confirm panel (release is irreversible).
 * NO_LINES / BOM_REVISION_CONFLICT surface in the panel; the conflict banner
 * gains a reload-to-latest link. Button disables without pdm.bom.approve.
 */
export function ReleasePanel({
  lang,
  dict,
  styleId,
  bomId,
  canApprove,
}: {
  lang: Lang;
  dict: Dict;
  styleId: string;
  bomId: string;
  canApprove: boolean;
}) {
  const t = dict.boms;
  const [state, formAction, pending] = useActionState(release.bind(null, lang, styleId, bomId), INITIAL);
  const [open, setOpen] = useState(false);
  const reloadHref = `/${lang}/styles/${styleId}/bom`;

  return (
    <div>
      {!open && (
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => setOpen(true)}
            disabled={!canApprove}
            className="flex h-10 items-center gap-2 rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90 disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            {t.release.release}
          </button>
          {!canApprove && <p className="text-xs text-sidebar-foreground">{t.perm.approveHint}</p>}
        </div>
      )}
      {open && (
        <div className="rounded-lg border border-border bg-card p-4">
          <div className="flex gap-3">
            <span aria-hidden="true" className="mt-0.5 text-warning">
              <AlertIcon />
            </span>
            <div className="min-w-0">
              <p className="text-sm font-medium">{t.release.confirmTitle}</p>
              <p className="mt-0.5 text-sm text-sidebar-foreground">{t.release.confirmBody}</p>
            </div>
          </div>
          {state.status === "error" && state.message && (
            <ActionBanner message={state.message} conflict={state.conflict} reloadHref={reloadHref} reloadLabel={t.errors.reload} />
          )}
          <div className="mt-4 flex items-center justify-end gap-2">
            <button
              type="button"
              onClick={() => setOpen(false)}
              className="flex h-10 items-center rounded-lg border border-border px-4 text-sm font-medium text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
            >
              {t.release.cancel}
            </button>
            <form action={formAction}>
              <button
                type="submit"
                disabled={pending}
                className="flex h-10 items-center rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90 disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
              >
                {pending ? t.release.releasing : t.release.confirm}
              </button>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

/**
 * Create-BOM panel for a style version without one. BOM_EXISTS (a concurrent
 * create) renders inline; button disables without pdm.bom.create, with hint.
 */
export function CreateBomPanel({
  lang,
  dict,
  styleId,
  styleVersionId,
  canCreate,
}: {
  lang: Lang;
  dict: Dict;
  styleId: string;
  styleVersionId: string;
  canCreate: boolean;
}) {
  const t = dict.boms;
  const [state, formAction, pending] = useActionState(createBom.bind(null, lang, styleId, styleVersionId), INITIAL);
  return (
    <section aria-label={t.create.whenNone} className="rounded-lg border border-border bg-card p-6">
      <p className="text-sm text-sidebar-foreground">{t.create.whenNone}</p>
      {state.status === "error" && state.message && (
        <ActionBanner message={state.message} />
      )}
      <form action={formAction} className="mt-4">
        <button
          type="submit"
          disabled={pending || !canCreate}
          className="flex h-10 items-center gap-2 rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90 disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          {pending ? t.create.creating : t.create.createBom}
        </button>
      </form>
      {!canCreate && <p className="mt-3 text-xs text-sidebar-foreground">{t.perm.createHint}</p>}
    </section>
  );
}
