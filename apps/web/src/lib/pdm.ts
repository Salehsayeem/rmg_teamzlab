/**
 * PDM style + BOM read models, mirroring the API contracts (PdmDtos.cs) — camelCase
 * as the API serializes them. Types only, so both server components and the
 * "use server" actions module can import them (actions may only export functions).
 */

export type StyleVariantDto = { id: string; colorCode: string; sizeCode: string };

export type StyleVersionDto = {
  id: string;
  version: number;
  status: string;
  approvedAtUtc: string | null;
  variants: StyleVariantDto[];
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

export type BomDto = {
  id: string;
  styleVersionId: string;
  status: string;
  revision: number;
};

export type BuyerOption = { id: string; code: string; name: string };

/** Result shape shared by the style server actions; null means "no outcome yet". */
export type StyleActionResult = {
  /** Stable API problem code (STYLE_CODE_TAKEN, …) or "OK". */
  code?: string;
  /** Flat problem messages (banner details). */
  errors?: string[];
  /** Per-field messages rendered under the matching inputs. */
  fields?: Record<string, string[]>;
  /** Fresh revision after a 409 STYLE_REVISION_CONFLICT. */
  conflictRevision?: number;
};
