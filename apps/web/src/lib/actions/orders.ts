"use server";

import { redirect } from "next/navigation";
import { ApiError, apiFetch } from "@/lib/api";
import { can } from "@/lib/perm";
import { getDict, type Lang } from "@/lib/i18n/getDict";
import type { OrderDto } from "@/components/orders/OrderStates";

export type OrderFormState = {
  status: "idle" | "error" | "conflict";
  /** Banner-level message (network / unexpected / state failures). */
  message?: string;
  /** Field-keyed messages (VALIDATION_FAILED / ORDER_VARIANT_MISMATCH errors object). */
  fieldErrors?: Record<string, string[]>;
  /** ORDER_REVISION_CONFLICT: the current revision refetched after the 409. */
  currentRevision?: number;
};

/** Serialized capture payload the client form builds (matches OrderCreateRequest). */
export type OrderPayload = {
  code: string;
  buyerId: string;
  buyerPo: string;
  currency: string;
  orderDate: string;
  lines: {
    styleVersionId: string;
    price: number;
    sizes: { colorCode: string; sizeCode: string; qty: number }[];
    schedules: { shipDate: string; qty: number }[];
  }[];
};

/**
 * Creates a customer order via POST /ord/customer-orders. ord.order.create is
 * re-checked here so direct POSTs to the action cannot create. Every submit
 * sends a fresh Idempotency-Key (crypto.randomUUID) so a retry after a lost
 * response cannot double-create. Success navigates to the detail page:
 * redirect() unwinds via a thrown sentinel, so it sits outside the try.
 */
export async function createOrder(
  lang: Lang,
  _prevState: OrderFormState,
  formData: FormData,
): Promise<OrderFormState> {
  if (!(await can("ord.order.create"))) {
    return { status: "error", message: getDict(lang).orders.errors.generic };
  }

  let payload: OrderPayload;
  try {
    payload = JSON.parse(String(formData.get("payload"))) as OrderPayload;
  } catch {
    return { status: "error", message: getDict(lang).orders.errors.network };
  }

  let newId: string;
  try {
    const { data } = await apiFetch<OrderDto>("/ord/customer-orders", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Idempotency-Key": crypto.randomUUID(),
      },
      body: JSON.stringify(payload),
    });
    newId = data.id;
  } catch (e) {
    if (e instanceof ApiError) {
      const t = getDict(lang).orders.errors;
      if (e.code === "DUPLICATE_BUYER_PO") {
        return { status: "error", fieldErrors: { buyerPo: [t.duplicateBuyerPo] } };
      }
      if (e.code === "STYLE_VERSION_NOT_APPROVED") {
        return { status: "error", message: t.styleVersionNotApproved };
      }
      if (e.code === "ORDER_VARIANT_MISMATCH") {
        return { status: "error", message: t.variantMismatch, fieldErrors: e.fieldErrors };
      }
      return {
        status: "error",
        message: e.code === "VALIDATION_FAILED" ? t.validationFailed : t.generic,
        fieldErrors: e.fieldErrors,
      };
    }
    return { status: "error", message: getDict(lang).orders.errors.network };
  }

  redirect(`/${lang}/orders/${newId}`);
}

export type ConfirmState = {
  status: "idle" | "error" | "conflict";
  message?: string;
  /** ORDER_REVISION_CONFLICT: current revision, refetched from the API after the 409. */
  currentRevision?: number;
};

/**
 * Confirms an order via POST /ord/customer-orders/{id}/confirm with the loaded
 * detail's revision as expectedRevision. The endpoint is [Idempotent] and the
 * submit carries a fresh Idempotency-Key, so a retry after a lost response
 * replays the first result instead of double-writing. A 409
 * ORDER_REVISION_CONFLICT refetches the order so the banner can offer a
 * refresh to the current revision. Success redirects back to the detail page.
 */
export async function confirmOrder(
  lang: Lang,
  id: string,
  expectedRevision: number,
): Promise<ConfirmState> {
  if (!(await can("ord.order.confirm"))) {
    return { status: "error", message: getDict(lang).orders.errors.generic };
  }
  try {
    await apiFetch<OrderDto>(`/ord/customer-orders/${id}/confirm`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Idempotency-Key": crypto.randomUUID(),
      },
      body: JSON.stringify({ expectedRevision }),
    });
  } catch (e) {
    if (e instanceof ApiError) {
      const t = getDict(lang).orders.errors;
      if (e.code === "ORDER_REVISION_CONFLICT") {
        // The stale tab cannot know the current revision — refetch it for the banner.
        let currentRevision: number | undefined;
        try {
          currentRevision = (await apiFetch<OrderDto>(`/ord/customer-orders/${id}`)).data.revision;
        } catch {
          // banner still renders; the refresh link reloads the page anyway
        }
        return { status: "conflict", currentRevision };
      }
      if (e.code === "INVALID_STATE") return { status: "error", message: t.invalidState };
      if (e.code === "MAKER_CHECKER_VIOLATION") return { status: "error", message: t.makerChecker };
      return { status: "error", message: t.generic };
    }
    return { status: "error", message: getDict(lang).orders.errors.network };
  }
  redirect(`/${lang}/orders/${id}`);
}

/**
 * Cancels an order via POST /ord/customer-orders/{id}/cancel (Draft/Confirmed
 * only — the API answers 422 INVALID_STATE otherwise). ord.order.edit is
 * re-checked here. Success redirects back to the detail page.
 */
export async function cancelOrder(
  lang: Lang,
  id: string,
  _prevState: OrderFormState,
  formData: FormData,
): Promise<OrderFormState> {
  if (!(await can("ord.order.edit"))) {
    return { status: "error", message: getDict(lang).orders.errors.generic };
  }
  try {
    await apiFetch<OrderDto>(`/ord/customer-orders/${id}/cancel`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ reason: formData.get("reason") }),
    });
  } catch (e) {
    if (e instanceof ApiError) {
      const t = getDict(lang).orders.errors;
      if (e.code === "INVALID_STATE") return { status: "error", message: t.invalidState };
      return { status: "error", message: t.generic };
    }
    return { status: "error", message: getDict(lang).orders.errors.network };
  }
  redirect(`/${lang}/orders/${id}`);
}
