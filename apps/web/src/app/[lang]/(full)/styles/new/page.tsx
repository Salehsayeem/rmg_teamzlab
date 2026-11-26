import Link from "next/link";
import { notFound } from "next/navigation";
import { apiFetch } from "@/lib/api";
import { getPerms } from "@/lib/auth";
import { StyleForm } from "@/components/styles/StyleForm";
import { AlertIcon } from "@/components/icons";
import { getDict, isLang, type Lang } from "@/lib/i18n/getDict";
import type { BuyerOption } from "@/lib/pdm";

/**
 * New-style page (Task 20): gated on pdm.style.create — without it the page is
 * a notice, never a doomed form. Loads the buyer picker options from
 * /ord/buyers (read failure degrades to an empty picker with a hint).
 */
// Blocking route: the session-cookie reads cannot sit behind Suspense here
// (documented escape hatch, cf. the buyers screens).
export const instant = false;

export default async function NewStylePage({ params }: { params: Promise<{ lang: Lang }> }) {
  const { lang } = await params;
  if (!isLang(lang)) notFound();
  const dict = getDict(lang);
  const perms = await getPerms();
  const canCreate = perms.includes("pdm.style.create");

  let buyers: BuyerOption[] = [];
  let buyersFailed = false;
  try {
    const { data: rows } = await apiFetch<{ id: string; code: string; name: string }[]>("/ord/buyers?pageSize=100");
    buyers = rows.map((b) => ({ id: b.id, code: b.code, name: b.name }));
  } catch {
    buyersFailed = true;
  }

  return (
    <div className="flex flex-col gap-6">
      <header>
        <Link href={`/${lang}/styles`} className="text-sm text-sidebar-foreground transition-colors hover:text-foreground">
          {dict.styles.backToList}
        </Link>
        <h1 className="mt-1 text-xl font-semibold tracking-tight">{dict.styles.form.title}</h1>
        <p className="mt-1 text-sm text-sidebar-foreground">{dict.styles.form.subtitle}</p>
      </header>
      {!canCreate || buyersFailed ? (
        <div role="alert" className="flex max-w-2xl gap-3 rounded-lg bg-lighterror p-4">
          <span aria-hidden="true" className="mt-0.5 text-error">
            <AlertIcon />
          </span>
          <p className="text-sm">
            {!canCreate ? dict.styles.form.errors.permissionDenied : dict.styles.states.errorBody}
          </p>
        </div>
      ) : (
        <StyleForm lang={lang} dict={dict.styles} buyers={buyers} />
      )}
    </div>
  );
}
