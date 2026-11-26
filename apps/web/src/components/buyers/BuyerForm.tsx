"use client";

import { useActionState, useState } from "react";
import Link from "next/link";
import { AlertIcon, PlusIcon } from "@/components/icons";
import { createBuyer, type BuyerFormState } from "@/lib/actions/buyers";
import type { Dict, Lang } from "@/lib/i18n/getDict";

const CONTROL =
  "h-10 w-full rounded-lg border border-input bg-background px-3 text-sm text-foreground placeholder:text-sidebar-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";

const INITIAL: BuyerFormState = { status: "idle" };

export function BuyerForm({ lang, dict }: { lang: Lang; dict: Dict }) {
  const t = dict.buyers;
  const [state, formAction, pending] = useActionState(createBuyer.bind(null, lang), INITIAL);
  const [rows, setRows] = useState<number[]>([1]);

  const fieldError = (field: string) => state.fieldErrors?.[field]?.[0];
  const describedBy = (field: string, id: string) => (fieldError(field) ? id : undefined);
  const contactErrors = ["contacts", "contacts.email"].flatMap((k) => state.fieldErrors?.[k] ?? []);

  return (
    <section className="rounded-lg border border-border bg-card p-6">
      {state.status === "error" && state.message && (
        <div role="alert" className="mb-4 flex gap-3 rounded-lg bg-lighterror p-4">
          <span aria-hidden="true" className="mt-0.5 text-error">
            <AlertIcon />
          </span>
          <div className="min-w-0 text-sm">
            <p className="font-medium">{state.message}</p>
          </div>
        </div>
      )}
      <form action={formAction} className="flex flex-col gap-4" noValidate>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div>
            <label htmlFor="buyer-code" className="text-sm font-medium">
              {t.form.code}
            </label>
            <input
              id="buyer-code"
              name="code"
              type="text"
              required
              autoComplete="off"
              placeholder={t.form.codePlaceholder}
              aria-invalid={fieldError("code") ? true : undefined}
              aria-describedby={describedBy("code", "buyer-code-error")}
              className={`mt-1.5 ${CONTROL}`}
            />
            {fieldError("code") && (
              <p id="buyer-code-error" className="mt-1 text-xs text-error">
                {fieldError("code")}
              </p>
            )}
          </div>
          <div>
            <label htmlFor="buyer-name" className="text-sm font-medium">
              {t.form.name}
            </label>
            <input
              id="buyer-name"
              name="name"
              type="text"
              required
              autoComplete="off"
              placeholder={t.form.namePlaceholder}
              aria-invalid={fieldError("name") ? true : undefined}
              aria-describedby={describedBy("name", "buyer-name-error")}
              className={`mt-1.5 ${CONTROL}`}
            />
            {fieldError("name") && (
              <p id="buyer-name-error" className="mt-1 text-xs text-error">
                {fieldError("name")}
              </p>
            )}
          </div>
          <div>
            <label htmlFor="buyer-country" className="text-sm font-medium">
              {t.form.country}
            </label>
            <input
              id="buyer-country"
              name="country"
              type="text"
              required
              autoComplete="off"
              placeholder={t.form.countryPlaceholder}
              aria-invalid={fieldError("country") ? true : undefined}
              aria-describedby={describedBy("country", "buyer-country-error")}
              className={`mt-1.5 ${CONTROL}`}
            />
            {fieldError("country") && (
              <p id="buyer-country-error" className="mt-1 text-xs text-error">
                {fieldError("country")}
              </p>
            )}
          </div>
          <div>
            <label htmlFor="buyer-currency" className="text-sm font-medium">
              {t.form.currency}
            </label>
            <input
              id="buyer-currency"
              name="currency"
              type="text"
              required
              autoComplete="off"
              placeholder={t.form.currencyPlaceholder}
              aria-invalid={fieldError("currency") ? true : undefined}
              aria-describedby={describedBy("currency", "buyer-currency-error")}
              className={`mt-1.5 ${CONTROL}`}
            />
            {fieldError("currency") && (
              <p id="buyer-currency-error" className="mt-1 text-xs text-error">
                {fieldError("currency")}
              </p>
            )}
          </div>
        </div>
        <div>
          <p className="text-sm font-medium">{t.form.contacts}</p>
          <p className="mt-0.5 text-xs text-sidebar-foreground">{t.form.contactsHint}</p>
          {contactErrors.length > 0 && (
            <ul role="alert" className="mt-2 list-inside list-disc text-xs text-error">
              {contactErrors.map((msg) => (
                <li key={msg}>{msg}</li>
              ))}
            </ul>
          )}
          <div className="mt-3 flex flex-col gap-3">
            {rows.map((row) => (
              <div key={row} className="grid grid-cols-1 gap-3 sm:grid-cols-[1fr_1fr_1fr_auto]">
                <div className="sm:col-span-1">
                  <label htmlFor={`contact-name-${row}`} className="sr-only">
                    {t.form.contactName}
                  </label>
                  <input
                    id={`contact-name-${row}`}
                    name="contactName"
                    type="text"
                    autoComplete="off"
                    placeholder={t.form.contactName}
                    className={CONTROL}
                  />
                </div>
                <div>
                  <label htmlFor={`contact-email-${row}`} className="sr-only">
                    {t.form.contactEmail}
                  </label>
                  <input
                    id={`contact-email-${row}`}
                    name="contactEmail"
                    type="email"
                    autoComplete="off"
                    placeholder={t.form.contactEmail}
                    className={CONTROL}
                  />
                </div>
                <div>
                  <label htmlFor={`contact-phone-${row}`} className="sr-only">
                    {t.form.contactPhone}
                  </label>
                  <input
                    id={`contact-phone-${row}`}
                    name="contactPhone"
                    type="tel"
                    autoComplete="off"
                    placeholder={t.form.contactPhone}
                    className={CONTROL}
                  />
                </div>
                <button
                  type="button"
                  onClick={() => setRows(rows.filter((r) => r !== row))}
                  aria-label={`${t.form.removeContact} ${row}`}
                  className="flex h-10 items-center justify-center rounded-lg border border-border px-3 text-sm font-medium text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                >
                  {t.form.removeContact}
                </button>
              </div>
            ))}
          </div>
          <button
            type="button"
            onClick={() => setRows([...rows, Math.max(...rows) + 1])}
            className="mt-3 inline-flex h-10 items-center gap-2 rounded-lg border border-border px-3 text-sm font-medium text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            <PlusIcon />
            {t.form.addContact}
          </button>
        </div>
        <div className="mt-2 flex items-center justify-end gap-2">
          <Link
            href={`/${lang}/buyers`}
            className="flex h-10 items-center rounded-lg border border-border px-4 text-sm font-medium text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            {t.form.cancel}
          </Link>
          <button
            type="submit"
            disabled={pending}
            className="flex h-10 items-center justify-center rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90 disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            {pending ? t.form.submitting : t.form.submit}
          </button>
        </div>
      </form>
    </section>
  );
}
