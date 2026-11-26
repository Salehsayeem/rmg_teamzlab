"use client";

import { useState } from "react";
import { AlertIcon } from "@/components/icons";
import { styleErrorText } from "@/components/styles/error-keys";
import { approveStyleAction } from "@/lib/actions/styles";
import type { Dict } from "@/lib/i18n/getDict";

/**
 * Approve command button for a Draft style: calls the approve server action,
 * which refreshes the route so the status badge flips. Disabled (with a hint)
 * without the pdm.style.approve permission; API failures surface as a banner
 * keyed by the stable problem code — the 409 conflict variant points at the
 * revision to refresh to.
 */
export function ApproveButton({ id, dict, canApprove }: {
  id: string;
  dict: Dict["styles"];
  canApprove: boolean;
}) {
  const t = dict.detail;
  const [pending, setPending] = useState(false);
  const [banner, setBanner] = useState<{ message: string; details: string[] } | null>(null);

  async function onApprove() {
    if (pending) return;
    setPending(true);
    setBanner(null);
    const result = await approveStyleAction(id);
    setPending(false);
    if (result.code !== "OK") {
      setBanner(styleErrorText(result.code, result.conflictRevision, result.errors, dict.form.errors));
    }
  }

  return (
    <div className="flex flex-col items-start gap-2">
      <div className="flex items-center gap-2">
        <button
          type="button"
          onClick={onApprove}
          disabled={!canApprove || pending}
          title={canApprove ? undefined : dict.detail.noPermHint}
          className="flex h-10 items-center justify-center rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          {pending ? t.approving : t.approve}
        </button>
      </div>
      {banner && (
        <div role="alert" className="flex gap-3 rounded-lg bg-lighterror p-4">
          <span aria-hidden="true" className="mt-0.5 text-error">
            <AlertIcon />
          </span>
          <div className="min-w-0 text-sm">
            <p className="font-medium">{banner.message}</p>
            {banner.details.length > 0 && (
              <ul className="mt-1 list-inside list-disc text-sidebar-foreground">
                {banner.details.map((detail) => (
                  <li key={detail}>{detail}</li>
                ))}
              </ul>
            )}
          </div>
        </div>
      )}
      {!canApprove && <p className="text-xs text-sidebar-foreground">{t.noPermHint}</p>}
    </div>
  );
}
