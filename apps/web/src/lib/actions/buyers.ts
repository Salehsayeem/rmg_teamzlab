"use server";

import { redirect } from "next/navigation";
import { ApiError, apiFetch } from "@/lib/api";
import { can } from "@/lib/perm";
import { getDict, type Lang } from "@/lib/i18n/getDict";

export type BuyerFormState = {
  status: "idle" | "error";
  /** Banner-level message (network / unexpected failures). */
  message?: string;
  /** Field-keyed messages (VALIDATION_FAILED's errors object, plus BUYER_CODE_TAKEN mapped onto `code`). */
  fieldErrors?: Record<string, string[]>;
};

/**
 * Creates a buyer via POST /ord/buyers. ord.buyer.create is re-checked here so
 * direct POSTs to the action cannot create. Every submit sends a fresh
 * Idempotency-Key (crypto.randomUUID) so a retry after a lost response cannot
 * double-create. Success navigates to the detail page: redirect() unwinds via a
 * thrown sentinel, so the call sits outside the try below on purpose.
 */
export async function createBuyer(
  lang: Lang,
  _prevState: BuyerFormState,
  formData: FormData,
): Promise<BuyerFormState> {
  if (!(await can("ord.buyer.create"))) {
    return { status: "error", message: getDict(lang).buyers.errors.generic };
  }

  const names = formData.getAll("contactName").map(String);
  const emails = formData.getAll("contactEmail").map(String);
  const phones = formData.getAll("contactPhone").map(String);
  const contacts = names
    .map((name, i) => ({ name, email: emails[i] ?? "", phone: phones[i] ?? "" }))
    .filter((c) => c.name !== "" || c.email !== "" || c.phone !== "");

  let newId: string;
  try {
    const { data } = await apiFetch<{ id: string }>("/ord/buyers", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Idempotency-Key": crypto.randomUUID(),
      },
      body: JSON.stringify({
        code: formData.get("code"),
        name: formData.get("name"),
        country: formData.get("country"),
        currency: formData.get("currency"),
        contacts,
      }),
    });
    newId = data.id;
  } catch (e) {
    if (e instanceof ApiError) {
      const t = getDict(lang).buyers.errors;
      if (e.code === "BUYER_CODE_TAKEN") {
        return { status: "error", fieldErrors: { code: [t.codeTaken] } };
      }
      return {
        status: "error",
        message: e.code === "VALIDATION_FAILED" ? t.validationFailed : t.generic,
        fieldErrors: e.fieldErrors,
      };
    }
    return { status: "error", message: getDict(lang).buyers.errors.network };
  }

  redirect(`/${lang}/buyers/${newId}`);
}
