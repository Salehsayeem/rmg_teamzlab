"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { AlertIcon } from "@/components/icons";
import { styleErrorText } from "@/components/styles/error-keys";
import { createStyleAction } from "@/lib/actions/styles";
import type { BuyerOption } from "@/lib/pdm";
import type { Dict, Lang } from "@/lib/i18n/getDict";

const control =
  "h-10 w-full rounded-lg border border-input bg-background px-3 text-sm text-foreground placeholder:text-sidebar-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";
const chip =
  "inline-flex items-center gap-1.5 rounded-full bg-lightprimary px-2.5 py-1 text-xs font-medium text-primary";
const chipRemove =
  "rounded-full text-primary/70 transition-opacity hover:opacity-70 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";
const fieldError = "mt-1 text-xs text-error";

/** New-style form with the variant matrix editor: rows = color codes, columns =
 * size codes; every intersection is a variant of the style's first version.
 * The cartesian product goes into a hidden input for the create-style action,
 * which redirects to the detail page on success. */
export function StyleForm({ lang, dict, buyers }: {
  lang: Lang;
  dict: Dict["styles"];
  buyers: BuyerOption[];
}) {
  const t = dict.form;
  const [pending, setPending] = useState(false);
  const [banner, setBanner] = useState<{ message: string; details: string[] } | null>(null);
  const [fields, setFields] = useState<Record<string, string[]>>({});
  const [colors, setColors] = useState<string[]>([]);
  const [sizes, setSizes] = useState<string[]>([]);

  const variants = colors.flatMap((colorCode) =>
    sizes.map((sizeCode) => ({ colorCode, sizeCode })),
  );


  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending) return;
    const formData = new FormData(event.currentTarget);
    setPending(true);
    setBanner(null);
    setFields({});
    const result = await createStyleAction(lang, formData);
    setPending(false);
    if (result?.code) {
      setBanner(styleErrorText(result.code, result.conflictRevision, result.errors, t.errors));
      setFields(result.fields ?? {});
    }
  }


  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-6" noValidate>
      <input type="hidden" name="variants" value={JSON.stringify(variants)} />
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
      {buyers.length === 0 && <p className="text-sm text-error">{t.buyerMissing}</p>}
      <div className="max-w-2xl rounded-lg border border-border bg-card p-6">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div>
            <label htmlFor="style-code" className="text-sm font-medium">{t.code}</label>
            <input id="style-code" name="code" type="text" placeholder={t.codePlaceholder} className={`mt-1.5 ${control}`} />
            {fields.code?.map((m) => <p key={m} className={fieldError}>{m}</p>)}
          </div>
          <div>
            <label htmlFor="style-name" className="text-sm font-medium">{t.name}</label>
            <input id="style-name" name="name" type="text" placeholder={t.namePlaceholder} className={`mt-1.5 ${control}`} />
            {fields.name?.map((m) => <p key={m} className={fieldError}>{m}</p>)}
          </div>
          <div>
            <label htmlFor="style-season" className="text-sm font-medium">{t.season}</label>
            <input id="style-season" name="season" type="text" placeholder={t.seasonPlaceholder} className={`mt-1.5 ${control}`} />
            {fields.season?.map((m) => <p key={m} className={fieldError}>{m}</p>)}
          </div>
          <div>
            <label htmlFor="style-category" className="text-sm font-medium">{t.category}</label>
            <input id="style-category" name="category" type="text" placeholder={t.categoryPlaceholder} className={`mt-1.5 ${control}`} />
            {fields.category?.map((m) => <p key={m} className={fieldError}>{m}</p>)}
          </div>
          <div>
            <label htmlFor="style-buyer" className="text-sm font-medium">{t.buyer}</label>
            <select id="style-buyer" name="buyerId" className={`mt-1.5 ${control}`}>
              <option value="">{t.buyerPlaceholder}</option>
              {buyers.map((b) => (
                <option key={b.id} value={b.id}>{`${b.code} — ${b.name}`}</option>
              ))}
            </select>
            {fields.buyerId?.map((m) => <p key={m} className={fieldError}>{m}</p>)}
          </div>
        </div>

        <div className="mt-6 rounded-lg border border-border p-4">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <span className="text-sm font-medium">{t.matrix}</span>
            <span className="text-xs text-sidebar-foreground">
              {t.variantCount.replace("{n}", String(variants.length))}
            </span>
          </div>
          <p className="mt-1 text-xs text-sidebar-foreground">{t.matrixHint}</p>
          {fields.variants?.map((m) => <p key={m} className={fieldError}>{m}</p>)}

          <div className="mt-3 grid grid-cols-1 gap-4 sm:grid-cols-2">
            <ChipEditor codes={colors} setCodes={setColors} label={t.colors} placeholder={t.colorPlaceholder} addLabel={t.addColor} removeLabel={t.remove} />
            <ChipEditor codes={sizes} setCodes={setSizes} label={t.sizes} placeholder={t.sizePlaceholder} addLabel={t.addSize} removeLabel={t.remove} />
          </div>

          {variants.length > 0 ? (
            <div className="mt-4 overflow-x-auto">
              <table className="w-full border-collapse text-sm">
                <thead>
                  <tr>
                    <th scope="col" className="border-b border-border px-2 py-2" aria-label={t.colors} />
                    {sizes.map((s) => (
                      <th key={s} scope="col" className="border-b border-border px-2 py-2 text-start font-medium">{s}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {colors.map((c) => (
                    <tr key={c} className="border-b border-border last:border-b-0">
                      <th scope="row" className="px-2 py-2 text-start font-medium">{c}</th>
                      {sizes.map((s) => (
                        <td key={s} className="px-2 py-2">
                          <span className="flex items-center gap-2">
                            <span aria-hidden="true" className="size-3 rounded-sm bg-primary" />
                            <span className="sr-only">{`${c} / ${s}`}</span>
                          </span>
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="mt-3 text-sm text-sidebar-foreground">{t.matrixEmpty}</p>
          )}
        </div>

        <div className="mt-6 flex items-center gap-3">
          <button
            type="submit"
            disabled={pending || buyers.length === 0}
            className="flex h-10 items-center justify-center rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90 disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            {pending ? t.submitting : t.submit}
          </button>
          <Link
            href={`/${lang}/styles`}
            className="inline-flex h-10 items-center rounded-lg border border-input px-4 text-sm font-medium transition-opacity hover:opacity-80 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            {t.cancel}
          </Link>
        </div>
      </div>
    </form>
  );
}

/** Chip editor for one matrix axis (color codes / size codes): text input +
 * add button, added codes render as removable chips. Enter adds instead of
 * submitting the surrounding form. */
function ChipEditor({ codes, setCodes, label, placeholder, addLabel, removeLabel }: {
  codes: string[];
  setCodes: (next: string[]) => void;
  label: string;
  placeholder: string;
  addLabel: string;
  removeLabel: string;
}) {
  const [value, setValue] = useState("");
  function apply() {
    const code = value.trim();
    if (!code || codes.includes(code)) return;
    setCodes([...codes, code]);
    setValue("");
  }
  return (
    <div>
      <span className="text-sm font-medium">{label}</span>
      {codes.length > 0 && (
        <div className="mt-2 flex flex-wrap gap-2">
          {codes.map((code) => (
            <span key={code} className={chip}>
              {code}
              <button
                type="button"
                onClick={() => setCodes(codes.filter((c) => c !== code))}
                aria-label={`${removeLabel} ${code}`}
                className={chipRemove}
              >
                ×
              </button>
            </span>
          ))}
        </div>
      )}
      <div className="mt-2 flex gap-2">
        <input
          type="text"
          value={value}
          onChange={(e) => setValue(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              e.preventDefault();
              apply();
            }
          }}
          placeholder={placeholder}
          aria-label={label}
          className={control}
        />
        <button
          type="button"
          onClick={apply}
          className="inline-flex h-10 shrink-0 items-center rounded-lg border border-input px-4 text-sm font-medium transition-opacity hover:opacity-80 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          {addLabel}
        </button>
      </div>
    </div>
  );
}
