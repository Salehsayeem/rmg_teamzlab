import Link from "next/link";
import { ChevronLeftIcon } from "@/components/icons";
import { BuyerForm } from "@/components/buyers/BuyerForm";
import { getDict, type Lang } from "@/lib/i18n/getDict";

export default async function NewBuyerPage({ params }: { params: Promise<{ lang: Lang }> }) {
  const { lang } = await params;
  const dict = getDict(lang);
  const d = dict.buyers;

  return (
    <div className="flex flex-col gap-6">
      <div>
        <Link
          href={`/${lang}/buyers`}
          className="inline-flex h-10 items-center gap-1.5 text-sm font-medium text-sidebar-foreground transition-colors hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          <ChevronLeftIcon />
          {d.detail.back}
        </Link>
      </div>
      <header>
        <h1 className="text-xl font-semibold tracking-tight">{d.form.title}</h1>
        <p className="mt-1 text-sm text-sidebar-foreground">{d.form.subtitle}</p>
      </header>
      <BuyerForm lang={lang} dict={dict} />
    </div>
  );
}
