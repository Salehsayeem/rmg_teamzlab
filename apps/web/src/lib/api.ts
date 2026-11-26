import { cookies } from "next/headers";

export const SESSION_COOKIE = "rmg_session";

export const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000/api/v1";

/** RFC 9457 problem+json failure (stable `code` + `errors[]`, per plan global constraints / R-1). */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    readonly errors: string[] = [],
    /** Field-keyed messages from VALIDATION_FAILED (`errors` as an object, e.g. `{code: [...]}`). */
    readonly fieldErrors: Record<string, string[]> = {},
  ) {
    super(code);
    this.name = "ApiError";
  }

  /** Alias kept for the styles screens' consumer naming (same object). */
  get fields(): Record<string, string[]> {
    return this.fieldErrors;
  }
}

export type ApiResult<T> = { data: T; meta: Record<string, unknown> };

/**
 * Server-side fetch wrapper.
 * - Reads the httpOnly `rmg_session` cookie and forwards it as a bearer token.
 * - Base URL from `NEXT_PUBLIC_API_URL` (default http://localhost:5000/api/v1);
 *   `path` must start with "/" and is appended verbatim.
 * - Unwraps the `{ data, meta }` success envelope (R-1); throws ApiError from problem+json.
 */
export async function apiFetch<T>(path: string, init?: RequestInit): Promise<ApiResult<T>> {
  const token = (await cookies()).get(SESSION_COOKIE)?.value;
  const res = await fetch(`${API_URL}${path}`, {
    ...init,
    cache: "no-store",
    headers: {
      ...init?.headers,
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  });

  if (!res.ok) {
    const problem = (await res.json().catch(() => null)) as {
      code?: string;
      title?: string;
      errors?: unknown;
    } | null;
    const raw = problem?.errors;
    const errors = Array.isArray(raw)
      ? raw.map((e) => (typeof e === "string" ? e : JSON.stringify(e)))
      : [];
    // Dictionary-shaped errors (VALIDATION_FAILED / ValidationProblem: { field: [msg] }) surface
    // as per-field messages; non-array values coerce to single-message arrays.
    const fieldErrors: Record<string, string[]> = {};
    if (raw && typeof raw === "object" && !Array.isArray(raw)) {
      for (const [field, messages] of Object.entries(raw as Record<string, unknown>)) {
        fieldErrors[field] = Array.isArray(messages)
          ? messages.map((m) => (typeof m === "string" ? m : JSON.stringify(m)))
          : [typeof messages === "string" ? messages : JSON.stringify(messages)];
      }
    }
    throw new ApiError(res.status, problem?.code ?? problem?.title ?? "UNKNOWN", errors, fieldErrors);
  }

  const envelope = (await res.json()) as { data?: T; meta?: Record<string, unknown> };
  return { data: envelope.data as T, meta: envelope.meta ?? {} };
}
