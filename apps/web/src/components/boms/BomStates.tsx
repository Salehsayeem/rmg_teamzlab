import type { ReactNode } from "react";
import Link from "next/link";
import type { Dict } from "@/lib/i18n/getDict";

/**
 * API DTOs (src/Modules/ProductEngineering/Contracts/PdmDtos.cs). camelCase JSON
 * per the API's serializer settings; shapes mirror the C# records exactly.
 */
export type UomDto = { code: string; name: string };
export type MaterialDto = {
  id: string;
  code: string;
  name: string;
  category: string | null;
  baseUomCode: string;
};
export type BomLineDto = {
  id: string;
  materialId: string;
  qtyPerUnit: number;
  uomCode: string;
  wastePct: number;
};
export type BomDto = {
  id: string;
  styleVersionId: string;
  status: string;
  revision: number;
  lines: BomLineDto[];
};
export type StyleVersionDto = {
  id: string;
  version: number;
  status: string;
  approvedAtUtc: string | null;
  variants: { id: string; colorCode: string; sizeCode: string }[];
};
export type StyleDto = {
  id: string;
  code: string;
  name: string;
  season: string | null;
  category: string | null;
  buyerId: string;
  status: string;
  revision: number;
  versions: StyleVersionDto[];
};

/** Draft -> warning tint, Released -> success tint; mirrors the dashboard badge. */
export function BomStatusBadge({ status, dict }: { status: string; dict: Dict }) {
  const released = status === "Released";
  const label = released ? dict.boms.status.released : status === "Draft" ? dict.boms.status.draft : status;
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium text-sidebar-foreground ${released ? "bg-lightsuccess" : "bg-lightwarning"}`}
    >
      <span aria-hidden="true" className={`size-1.5 rounded-full ${released ? "bg-success" : "bg-warning"}`} />
      {label}
    </span>
  );
}

/** Loading skeleton mirroring the lines-table rows (role=status). */
export function BomSkeleton({ label }: { label: string }) {
  return (
    <div role="status" aria-label={label} className="flex flex-col gap-2">
      {[0, 1, 2, 3].map((i) => (
        <div key={i} className="flex h-10 items-center gap-4">
          <span className="size-4 animate-pulse rounded bg-muted" />
          <span className="h-4 w-28 animate-pulse rounded bg-muted" />
          <span className="ms-auto h-4 w-16 animate-pulse rounded bg-muted" />
        </div>
      ))}
    </div>
  );
}

/** Page-level failure (network / 5xx): alert + retry link back to this page. */
export function BomErrorState({
  title,
  body,
  icon,
  retryHref,
  retryLabel,
}: {
  title: string;
  body: string;
  icon: ReactNode;
  retryHref: string;
  retryLabel: string;
}) {
  return (
    <div role="alert" className="flex flex-col items-center gap-3 py-10 text-center">
      <span aria-hidden="true" className="flex size-12 items-center justify-center rounded-full bg-lighterror text-error">
        {icon}
      </span>
      <p className="text-sm font-medium">{title}</p>
      <p className="max-w-sm text-sm text-sidebar-foreground">{body}</p>
      <Link href={retryHref} className="text-sm font-medium text-primary hover:underline">
        {retryLabel}
      </Link>
    </div>
  );
}
