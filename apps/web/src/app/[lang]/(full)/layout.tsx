import { notFound } from "next/navigation";
import { Header } from "@/components/shell/Header";
import { Sidebar } from "@/components/shell/Sidebar";
import { getDict, isLang } from "@/lib/i18n/getDict";

/**
 * FullLayout shell: pinned 268px sidebar + sticky 72px header + footer credit.
 * Dictionary strings are loaded server-side here and handed to the client shell.
 */
export default async function FullLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ lang: string }>;
}) {
  const { lang } = await params;
  if (!isLang(lang)) notFound();
  const dict = getDict(lang);

  return (
    <div className="flex min-h-screen">
      <Sidebar lang={lang} dict={dict} />
      <div className="flex min-w-0 flex-1 flex-col">
        <Header lang={lang} dict={dict} />
        <main className="flex-1 p-6">{children}</main>
        <footer className="border-t border-border px-6 py-4 text-xs text-sidebar-foreground">
          {dict.footer.credit}
        </footer>
      </div>
    </div>
  );
}
