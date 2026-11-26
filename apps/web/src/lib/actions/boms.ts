"use server";

import { redirect } from "next/navigation";
import { ApiError, apiFetch } from "@/lib/api";
import { can } from "@/lib/perm";
import { getDict, type Lang } from "@/lib/i18n/getDict";

export type BomActionState = {
  status: "idle" | "error";
  /** Localized banner message (business rule / network failures). */
  message?: string;
  /** 409 BOM_REVISION_CONFLICT — the banner gains a reload-to-latest affordance. */
  conflict?: boolean;
  /** Field-keyed messages for inline display under the form controls. */
  fieldErrors?: Record<string, string[]>;
};

/**
 * Business codes with per-field payloads map onto the field they invalidate
 * (localized — the API only echoes the offending value); VALIDATION_FAILED
 * keeps its raw per-field API messages with a localized banner.
 */
function mapLineError(e: ApiError, t: ReturnType<typeof getDict>["boms"]["errors"]): BomActionState {
  if (e.code === "QTY_POSITIVE") {
    return { status: "error", fieldErrors: { qtyPerUnit: [t.qtyPositive] } };
  }
  if (e.code === "BOM_LINE_INVALID_UOM") {
    return { status: "error", fieldErrors: { uomCode: [t.uomInvalid] } };
  }
  if (e.code === "MATERIAL_INVALID") {
    return { status: "error", fieldErrors: { materialId: [t.materialInvalid] } };
  }
  if (e.code === "VALIDATION_FAILED") {
    return { status: "error", message: t.validationFailed, fieldErrors: e.fieldErrors };
  }
  if (e.code === "BOM_REVISION_CONFLICT") {
    return { status: "error", message: t.revisionConflict, conflict: true };
  }
  if (e.code === "INVALID_STATE") {
    return { status: "error", message: t.invalidState };
  }
  if (e.code === "MAKER_CHECKER_VIOLATION") {
    return { status: "error", message: t.makerChecker };
  }
  if (e.code === "NO_LINES") {
    return { status: "error", message: t.noLines };
  }
  return { status: "error", message: t.generic };
}

/**
 * Creates the style version's BOM (POST /pdm/boms). pdm.bom.create is re-checked
 * here so direct POSTs to the action cannot create. BOM_EXISTS renders inline
 * (one BOM per version). Success re-navigates to this page: redirect() unwinds
 * via a thrown sentinel, so the call sits outside the try below on purpose.
 */
export async function createBom(
  lang: Lang,
  styleId: string,
  styleVersionId: string,
  _prevState: BomActionState,
  _formData: FormData,
): Promise<BomActionState> {
  const t = getDict(lang).boms;
  if (!(await can("pdm.bom.create"))) return { status: "error", message: t.errors.generic };

  try {
    await apiFetch("/pdm/boms", {
      method: "POST",
      headers: { "Content-Type": "application/json", "Idempotency-Key": crypto.randomUUID() },
      body: JSON.stringify({ styleVersionId }),
    });
  } catch (e) {
    if (e instanceof ApiError) {
      return { status: "error", message: e.code === "BOM_EXISTS" ? t.errors.bomExists : t.errors.generic };
    }
    return { status: "error", message: t.errors.network };
  }

  redirect(`/${lang}/styles/${styleId}/bom`);
}

/**
 * Adds a line (POST /pdm/boms/{id}/lines) while the BOM is Draft. pdm.bom.edit is
 * re-checked server-side. Field-mapped business codes render inline under the
 * offending control; success re-navigates so the grid shows the new row.
 */
export async function addLine(
  lang: Lang,
  styleId: string,
  bomId: string,
  _prevState: BomActionState,
  formData: FormData,
): Promise<BomActionState> {
  const t = getDict(lang).boms;
  if (!(await can("pdm.bom.edit"))) return { status: "error", message: t.errors.generic };

  // Trust boundary: FormData is untrusted — parse + check before the API call.
  const materialId = String(formData.get("materialId") ?? "");
  const uomCode = String(formData.get("uomCode") ?? "").trim();
  const qty = Number(formData.get("qtyPerUnit"));
  const wasteRaw = String(formData.get("wastePct") ?? "").trim();
  const waste = wasteRaw === "" ? undefined : Number(wasteRaw);

  const fieldErrors: Record<string, string[]> = {};
  if (!materialId) fieldErrors.materialId = [t.add.materialPlaceholder];
  if (!uomCode) fieldErrors.uomCode = [t.add.uomPlaceholder];
  if (!Number.isFinite(qty) || qty <= 0) fieldErrors.qtyPerUnit = [t.errors.qtyPositive];
  if (waste !== undefined && (!Number.isFinite(waste) || waste < 0)) {
    fieldErrors.wastePct = [t.errors.validationFailed];
  }
  if (Object.keys(fieldErrors).length > 0) {
    return { status: "error", fieldErrors };
  }

  try {
    await apiFetch(`/pdm/boms/${bomId}/lines`, {
      method: "POST",
      headers: { "Content-Type": "application/json", "Idempotency-Key": crypto.randomUUID() },
      body: JSON.stringify({
        materialId,
        qtyPerUnit: qty,
        uomCode,
        ...(waste !== undefined ? { wastePct: waste } : {}),
      }),
    });
  } catch (e) {
    if (e instanceof ApiError) return mapLineError(e, t.errors);
    return { status: "error", message: t.errors.network };
  }

  redirect(`/${lang}/styles/${styleId}/bom`);
}

/**
 * Releases the BOM (POST /pdm/boms/{id}/approve) — irreversible, guarded by
 * pdm.bom.approve re-checked server-side. NO_LINES / BOM_REVISION_CONFLICT
 * render in the confirm panel; success re-navigates (status badge flips).
 */
export async function release(
  lang: Lang,
  styleId: string,
  bomId: string,
  _prevState: BomActionState,
  _formData: FormData,
): Promise<BomActionState> {
  const t = getDict(lang).boms;
  if (!(await can("pdm.bom.approve"))) return { status: "error", message: t.errors.generic };

  try {
    await apiFetch(`/pdm/boms/${bomId}/approve`, {
      method: "POST",
      headers: { "Idempotency-Key": crypto.randomUUID() },
    });
  } catch (e) {
    if (e instanceof ApiError) return mapLineError(e, t.errors);
    return { status: "error", message: t.errors.network };
  }

  redirect(`/${lang}/styles/${styleId}/bom`);
}
