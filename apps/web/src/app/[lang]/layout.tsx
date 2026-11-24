import type { Metadata } from "next";
import { DM_Sans, Noto_Sans_Bengali } from "next/font/google";
import { notFound } from "next/navigation";
import "../globals.css";
import { ThemeProvider } from "@/components/theme/ThemeProvider";
import { getDict, isLang, langs, type Lang } from "@/lib/i18n/getDict";

const dmSans = DM_Sans({ subsets: ["latin"], variable: "--font-dm-sans", display: "swap" });
const notoBengali = Noto_Sans_Bengali({ subsets: ["bengali"], variable: "--font-noto-bengali", display: "swap" });


export function generateStaticParams(): { lang: Lang }[] {
  return langs.map((lang) => ({ lang }));
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ lang: string }>;
}): Promise<Metadata> {
  const { lang } = await params;
  return { title: getDict(isLang(lang) ? lang : "en").app.name };
}

/** Seeds the dark class before paint so dark mode never flashes light. */
const themeInitScript = `try{if(localStorage.getItem("rmg-theme")==="dark")document.documentElement.classList.add("dark")}catch(e){}`;

export default async function RootLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ lang: string }>;
}) {
  const { lang } = await params;
  if (!isLang(lang)) notFound();

  return (
    <html
      lang={lang}
      dir="ltr"
      className={`${dmSans.variable} ${notoBengali.variable}`}
      suppressHydrationWarning
    >
      <body className="antialiased">
        <script dangerouslySetInnerHTML={{ __html: themeInitScript }} />
        <ThemeProvider>{children}</ThemeProvider>
      </body>
    </html>
  );
}
