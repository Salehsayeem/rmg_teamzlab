import type { Dict } from "@/lib/i18n/getDict";

type StyleStatus = keyof Dict["styles"]["status"]; // "draft" | "approved"

/** API status string -> dict label; unknown statuses fall back to the raw string. */
export function statusLabel(status: string, labels: Dict["styles"]["status"]): string {
  const key = status.toLowerCase() as StyleStatus;
  return labels[key] ?? status;
}

/**
 * Style status badge: Draft = neutral tint, Approved = success tint (per the
 * badge tints already used on the dashboard). Unknown statuses stay neutral.
 */
export function StatusBadge({ status, labels }: { status: string; labels: Dict["styles"]["status"] }) {
  const approved = status.toLowerCase() === "approved";
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium text-sidebar-foreground ${approved ? "bg-lightsuccess" : "bg-muted"}`}
    >
      <span aria-hidden="true" className={`size-1.5 rounded-full ${approved ? "bg-success" : "bg-muted-foreground"}`} />
      {statusLabel(status, labels)}
    </span>
  );
}
