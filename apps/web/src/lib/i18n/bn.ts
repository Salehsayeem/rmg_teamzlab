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
    logout: "সাইন আউট",
  },
  login: {
    title: "সাইন ইন",
    subtitle: "আপনার RMG অপারেশনস ওয়ার্কস্পেসে সাইন ইন করুন",
    email: { label: "ইমেইল", placeholder: "you@company.com" },
    password: { label: "পাসওয়ার্ড", placeholder: "আপনার পাসওয়ার্ড লিখুন" },
    submit: "সাইন ইন",
    submitting: "সাইন ইন হচ্ছে…",
    errors: {
      invalidCredentials: "ইমেইল বা পাসওয়ার্ড সঠিক নয়।",
      rateLimited: "অতিরিক্ত চেষ্টা হয়েছে। এক মিনিট অপেক্ষা করে আবার চেষ্টা করুন।",
      network: "প্রমাণীকরণ সার্ভিসে পৌঁছানো যাচ্ছে না। আবার চেষ্টা করুন।",
      generic: "সাইন ইন ব্যর্থ হয়েছে। আবার চেষ্টা করুন।",
    },
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
  boms: {
    title: "বিওএম (BOM)",
    versionLabel: "ভার্সন",
    status: { draft: "খসড়া", released: "প্রকাশিত" },
    table: { material: "মাল", qty: "একক প্রতি পরিমাণ", uom: "একক (UoM)", waste: "অপচয় (%)" },
    linesTitle: "লাইনসমূহ",
    linesEmpty: "এখনও কোনো লাইন নেই। নিচে প্রথম মাল যোগ করুন।",
    states: {
      loading: "বিওএম লোড হচ্ছে…",
      error: "কিছু একটা ভুল হয়েছে",
      errorBody: "বিওএম সার্ভিস এই মুহূর্তে ব্যবহার করা যাচ্ছে না।",
      retry: "আবার চেষ্টা করুন",
    },
    create: {
      createBom: "বিওএম তৈরি করুন",
      whenNone: "এই স্টাইল ভার্সনের এখনও কোনো বিওএম নেই।",
      creating: "তৈরি হচ্ছে…",
    },
    add: {
      title: "লাইন যোগ করুন",
      material: "মাল",
      materialPlaceholder: "একটি মাল নির্বাচন করুন",
      qty: "একক প্রতি পরিমাণ",
      uom: "একক (UoM)",
      uomPlaceholder: "একটি একক নির্বাচন করুন",
      waste: "অপচয় (%)",
      add: "লাইন যোগ করুন",
      adding: "যোগ হচ্ছে…",
    },
    release: {
      release: "বিওএম প্রকাশ করুন",
      releasing: "প্রকাশ হচ্ছে…",
      confirmTitle: "এই বিওএম প্রকাশ করবেন?",
      confirmBody: "প্রকাশ অপরিবর্তনীয় — প্রকাশিত বিওএম আর সম্পাদনা করা যাবে না।",
      confirm: "প্রকাশ নিশ্চিত করুন",
      cancel: "বাতিল",
    },
    perm: {
      createHint: "বিওএম তৈরির জন্য Create BOMs অনুমতি প্রয়োজন।",
      editHint: "লাইন যোগ করার জন্য Edit BOMs অনুমতি প্রয়োজন।",
      approveHint: "প্রকাশের জন্য Approve BOMs অনুমতি প্রয়োজন।",
    },
    errors: {
      bomExists: "এই স্টাইল ভার্সনের ইতিমধ্যে একটি বিওএম আছে (প্রতি ভার্সনে একটি)।",
      noLines: "প্রকাশের আগে অন্তত একটি লাইন যোগ করুন।",
      invalidState: "এই বিওএম আর পরিবর্তন করা যাবে না।",
      makerChecker: "মেকার-চেকার চালু আছে: নির্মাতা নিজের বিওএম প্রকাশ করতে পারবেন না।",
      revisionConflict: "অন্য কেউ এই বিওএম পরিবর্তন করেছে। সর্বশেষ অবস্থা দেখতে পুনরায় লোড করুন।",
      materialInvalid: "অজানা মাল।",
      uomInvalid: "অজানা একক।",
      qtyPositive: "একক প্রতি পরিমাণ শূন্যের চেয়ে বেশি হতে হবে।",
      validationFailed: "চিহ্নিত ঘরগুলো দেখুন।",
      generic: "কিছু একটা ভুল হয়েছে। আবার চেষ্টা করুন।",
      network: "ওয়েব সার্ভিসে পৌঁছানো যাচ্ছে না। আবার চেষ্টা করুন।",
      reload: "পুনরায় লোড",
    },
  },
};
