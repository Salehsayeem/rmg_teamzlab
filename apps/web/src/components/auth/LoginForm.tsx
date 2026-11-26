"use client";

import { useState, type FormEvent } from "react";
import { AlertIcon } from "@/components/icons";
import type { Dict, Lang } from "@/lib/i18n/getDict";

/** Problem code -> dict key for the banner message; unknown codes fall back to generic. */
const ERROR_KEY: Record<string, keyof Dict["login"]["errors"]> = {
  INVALID_CREDENTIALS: "invalidCredentials",
  RATE_LIMITED: "rateLimited",
  NETWORK: "network",
};

export function LoginForm({ lang, dict }: { lang: Lang; dict: Dict }) {
  const t = dict.login;
  const [pending, setPending] = useState(false);
  const [banner, setBanner] = useState<{ message: string; details: string[] } | null>(null);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending) return;
    const data = new FormData(event.currentTarget);
    setPending(true);
    setBanner(null);
    try {
      const res = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: data.get("email"), password: data.get("password") }),
      });
      if (res.ok) {
        // Full document navigation: the session cookie must be re-read server-side,
        // and a fresh page guarantees no client form state survives the auth boundary
        // (router.replace kept this component mounted with its pending state).
        // eslint-disable-next-line @next/next/no-location-assign-relative-destination
        window.location.assign(`/${lang}/dashboard`);
        return;
      }
      const problem = (await res.json().catch(() => null)) as {
        code?: string;
        errors?: string[];
      } | null;
      const key = problem?.code ? ERROR_KEY[problem.code] : undefined;
      setBanner({ message: t.errors[key ?? "generic"], details: problem?.errors ?? [] });
    } catch {
      setBanner({ message: t.errors.network, details: [] });
    }
    setPending(false);
  }

  const control =
    "h-10 w-full rounded-lg border border-input bg-background px-3 text-sm text-foreground placeholder:text-sidebar-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";

  return (
    <div className="flex min-h-screen items-center justify-center p-6">
      <div className="w-full max-w-[400px]">
        <div className="flex items-center gap-2.5 px-2 pb-6">
          <span
            aria-hidden="true"
            className="flex size-9 items-center justify-center rounded-lg bg-primary text-sm font-semibold text-primary-foreground"
          >
            {dict.app.mark}
          </span>
          <span className="text-base font-semibold tracking-tight">{dict.app.name}</span>
        </div>
        <div className="rounded-lg border border-border bg-card p-6">
          <h1 className="text-xl font-semibold tracking-tight">{t.title}</h1>
          <p className="mt-1 text-sm text-sidebar-foreground">{t.subtitle}</p>
          {banner && (
            <div role="alert" className="mt-4 flex gap-3 rounded-lg bg-lighterror p-4">
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
          <form onSubmit={onSubmit} className="mt-6 flex flex-col gap-4" noValidate>
            <div>
              <label htmlFor="login-email" className="text-sm font-medium">
                {t.email.label}
              </label>
              <input
                id="login-email"
                name="email"
                type="email"
                autoComplete="email"
                required
                placeholder={t.email.placeholder}
                className={`mt-1.5 ${control}`}
              />
            </div>
            <div>
              <label htmlFor="login-password" className="text-sm font-medium">
                {t.password.label}
              </label>
              <input
                id="login-password"
                name="password"
                type="password"
                autoComplete="current-password"
                required
                placeholder={t.password.placeholder}
                className={`mt-1.5 ${control}`}
              />
            </div>
            <button
              type="submit"
              disabled={pending}
              className="mt-2 flex h-10 items-center justify-center rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90 disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
            >
              {pending ? t.submitting : t.submit}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
