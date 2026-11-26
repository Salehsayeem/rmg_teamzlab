/**
 * English dictionary. The shape of this object IS the `Dict` type — bn.ts must
 * implement the identical key set, checked at compile time via `Record<Lang, Dict>`
 * in getDict.ts. Every UI string is keyed here; no hardcoded UI text in components.
 */
export const en = {
  app: {
    name: "RMG Operations",
    mark: "R",
  },
  nav: {
    label: "Main navigation",
    dashboard: "Dashboard",
    buyers: "Buyers",
    styles: "Styles",
    boms: "BOMs",
    orders: "Orders",
  },
  header: {
    searchLabel: "Global search",
    searchPlaceholder: "Search…",
    themeToggle: "Toggle theme",
    langToggle: "Switch language",
    langSwitchTo: "বাংলা",
    avatar: "Account",
    logout: "Sign out",
  },
  login: {
    title: "Sign in",
    subtitle: "Sign in to your RMG Operations workspace",
    email: { label: "Email", placeholder: "you@company.com" },
    password: { label: "Password", placeholder: "Enter your password" },
    submit: "Sign in",
    submitting: "Signing in…",
    errors: {
      invalidCredentials: "Invalid email or password.",
      rateLimited: "Too many attempts. Please wait a minute and try again.",
      network: "Cannot reach the authentication service. Please try again.",
      generic: "Sign-in failed. Please try again.",
    },
  },
  dashboard: {
    title: "Dashboard",
    subtitle: "Pilot workspace overview",
    stats: {
      buyers: { label: "Buyers", hint: "Active buyer accounts" },
      styles: { label: "Styles", hint: "Styles in development" },
      boms: { label: "BOMs", hint: "Released bills of materials" },
      orders: { label: "Orders", hint: "Customer orders this season" },
    },
    recentOrders: {
      title: "Recent orders",
      hint: "Latest customer orders across facilities",
    },
    table: { order: "Order", buyer: "Buyer", qty: "Qty", status: "Status" },
    status: { confirmed: "Confirmed", inProduction: "In production", pending: "Pending" },
    states: {
      loading: "Loading dashboard…",
      empty: "No orders yet",
      emptyBody: "Confirmed customer orders will appear here once capture begins.",
      error: "Something went wrong",
      errorBody: "The dashboard service is not reachable right now.",
    },
  },
  common: {
    valuePlaceholder: "—",
  },
  notFound: {
    title: "Page not found",
    body: "The page you are looking for does not exist or has moved.",
    toEn: "Dashboard (English)",
    toBn: "ড্যাশবোর্ড (বাংলা)",
  },
  footer: {
    credit: "© TeamzLab Demo Ltd. — RMG Operations Platform",
  },
};
