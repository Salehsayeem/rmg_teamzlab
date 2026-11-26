import type { Dict } from "@/lib/i18n/getDict";

/** Problem code -> dict key for banner messages; unknown codes fall back to generic ("unknown"). */
const STYLE_ERROR_KEY: Record<string, keyof Dict["styles"]["form"]["errors"]> = {
  STYLE_CODE_TAKEN: "styleCodeTaken",
  STYLE_VARIANT_DUPLICATE: "styleVariantDuplicate",
  NO_VARIANTS: "noVariants",
  MAKER_CHECKER_VIOLATION: "makerCheckerViolation",
  INVALID_STATE: "invalidState",
  PERMISSION_DENIED: "permissionDenied",
  STYLE_REVISION_CONFLICT: "styleRevisionConflict",
  NETWORK: "network",
};

/**
 * Resolves a style action outcome into banner text: localized message for the
 * code (with the conflict revision substituted for STYLE_REVISION_CONFLICT)
 * plus the API's flat messages as details. Mirrors the LoginForm code mapping.
 */
export function styleErrorText(
  code: string | undefined,
  rev: number | undefined,
  details: string[] | undefined,
  t: Dict["styles"]["form"]["errors"],
): { message: string; details: string[] } {
  const key = code ? STYLE_ERROR_KEY[code] : undefined;
  const raw = t[key ?? "unknown"];
  return {
    message: raw.replace("{n}", rev !== undefined ? String(rev) : "?"),
    details: details ?? [],
  };
}
