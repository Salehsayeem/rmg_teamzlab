/**
 * Bengali dictionary — implements the exact key set of `en` (compile-time
 * checked via `Record<Lang, Dict>` in getDict.ts).
 */
import type { Dict } from "./getDict";

export const bn: Dict = {
  app: {
    name: "RMG অপারেশনস",
    mark: "R",
  },
  nav: {
    label: "প্রধান নেভিগেশন",
    dashboard: "ড্যাশবোর্ড",
    buyers: "ক্রেতারা",
    styles: "স্টাইল",
    boms: "বিওএম",
    orders: "অর্ডার",
  },
  header: {
    searchLabel: "গ্লোবাল অনুসন্ধান",
    searchPlaceholder: "অনুসন্ধান…",
    themeToggle: "থিম পরিবর্তন",
    langToggle: "ভাষা পরিবর্তন",
    langSwitchTo: "EN",
    avatar: "অ্যাকাউন্ট",
  },
  dashboard: {
    title: "ড্যাশবোর্ড",
    subtitle: "পাইলট ওয়ার্কস্পেসের সারসংক্ষেপ",
    stats: {
      buyers: { label: "ক্রেতারা", hint: "সক্রিয় ক্রেতা অ্যাকাউন্ট" },
      styles: { label: "স্টাইল", hint: "উন্নয়নাধীন স্টাইল" },
      boms: { label: "বিওএম", hint: "প্রকাশিত বিওএমসমূহ" },
      orders: { label: "অর্ডার", hint: "এই মৌসুমের ক্রেতা অর্ডার" },
    },
    recentOrders: {
      title: "সাম্প্রতিক অর্ডার",
      hint: "সব কারখানার সাম্প্রতিক ক্রেতা অর্ডার",
    },
    table: { order: "অর্ডার", buyer: "ক্রেতা", qty: "পরিমাণ", status: "অবস্থা" },
    status: { confirmed: "নিশ্চিতকৃত", inProduction: "উৎপাদনে", pending: "অপেক্ষমাণ" },
    states: {
      loading: "ড্যাশবোর্ড লোড হচ্ছে…",
      empty: "এখনও কোনো অর্ডার নেই",
      emptyBody: "অর্ডার গ্রহণ শুরু হলে নিশ্চিতকৃত ক্রেতা অর্ডার এখানে দেখা যাবে।",
      error: "কিছু একটা ভুল হয়েছে",
      errorBody: "ড্যাশবোর্ড সার্ভিস এই মুহূর্তে ব্যবহার করা যাচ্ছে না।",
    },
  },
  common: {
    valuePlaceholder: "—",
  },
  notFound: {
    title: "পৃষ্ঠাটি পাওয়া যায়নি",
    body: "আপনি যে পৃষ্ঠাটি খুঁজছেন সেটি নেই বা সরিয়ে ফেলা হয়েছে।",
    toEn: "Dashboard (English)",
    toBn: "ড্যাশবোর্ড (বাংলা)",
  },
  footer: {
    credit: "© টিমজল্যাব ডেমো লিমিটেড — RMG অপারেশনস প্ল্যাটফর্ম",
  },
};
