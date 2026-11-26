import { notFound } from "next/navigation";
import { LoginForm } from "@/components/auth/LoginForm";
import { getDict, isLang } from "@/lib/i18n/getDict";

/** BlankLayout screen (per ui-direction.md checklist): no shell, centered card. */
export default async function LoginPage({ params }: { params: Promise<{ lang: string }> }) {
  const { lang } = await params;
  if (!isLang(lang)) notFound();

  return <LoginForm lang={lang} dict={getDict(lang)} />;
}
