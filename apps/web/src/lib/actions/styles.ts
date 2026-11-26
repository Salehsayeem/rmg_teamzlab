"use server";

import { refresh } from "next/cache";
import { redirect } from "next/navigation";
import { apiFetch, ApiError } from "@/lib/api";
import { getPerms } from "@/lib/auth";
import type { StyleActionResult, StyleDto } from "@/lib/pdm";

/**
 * Style command actions (Task 20). Both POSTs carry a fresh Idempotency-Key —
 * the API treats retries of the same key as replays. The web re-checks the
 * permission claim before calling the API (the API is the real gate; this only
 * avoids doomed round trips and gives the client a localizable code).
 */

export async function createStyleAction(lang: string, formData: FormData): Promise<StyleActionResult> {
  if (!(await getPerms()).includes("pdm.style.create")) return { code: "PERMISSION_DENIED" };

  const variants = JSON.parse(String(formData.get("variants") ?? "[]")) as {
    colorCode: string;
    sizeCode: string;
  }[];

  let created: StyleDto;
  try {
    ({ data: created } = await apiFetch<StyleDto>("/pdm/styles", {
      method: "POST",
      headers: { "Content-Type": "application/json", "Idempotency-Key": crypto.randomUUID() },
      body: JSON.stringify({
        code: formData.get("code"),
        name: formData.get("name"),
        season: formData.get("season") || null,
        category: formData.get("category") || null,
        buyerId: formData.get("buyerId"),
        variants,
      }),
    }));
  } catch (error) {
    if (error instanceof ApiError) {
      return { code: error.code, errors: error.errors, fields: error.fields };
    }
    return { code: "NETWORK" };
  }

  // redirect() throws NEXT_REDIRECT — outside the catch so it isn't swallowed.
  redirect(`/${lang}/styles/${created.id}`);
}

export async function approveStyleAction(id: string): Promise<StyleActionResult> {
  if (!(await getPerms()).includes("pdm.style.approve")) return { code: "PERMISSION_DENIED" };

  try {
    await apiFetch<StyleDto>(`/pdm/styles/${id}/approve`, {
      method: "POST",
      headers: { "Idempotency-Key": crypto.randomUUID() },
    });
  } catch (error) {
    if (error instanceof ApiError) {
      // Optimistic-concurrency loss: re-read the style so the banner can point
      // at the revision to refresh to; refresh() brings the page up to date.
      if (error.status === 409 && error.code === "STYLE_REVISION_CONFLICT") {
        try {
          const { data: fresh } = await apiFetch<StyleDto>(`/pdm/styles/${id}`);
          refresh();
          return { code: error.code, conflictRevision: fresh.revision };
        } catch {
          return { code: error.code };
        }
      }
      return { code: error.code, errors: error.errors, fields: error.fields };
    }
    return { code: "NETWORK" };
  }

  refresh();
  return { code: "OK" };
}
