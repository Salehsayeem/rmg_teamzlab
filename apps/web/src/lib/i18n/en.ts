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
  boms: {
    title: "Bill of materials",
    versionLabel: "Version",
    status: { draft: "Draft", released: "Released" },
    table: { material: "Material", qty: "Qty per unit", uom: "UoM", waste: "Waste %" },
    linesTitle: "Lines",
    linesEmpty: "No lines yet. Add the first material below.",
    states: {
      loading: "Loading BOM…",
      error: "Something went wrong",
      errorBody: "The BOM service is not reachable right now.",
      retry: "Retry",
    },
    create: {
      createBom: "Create BOM",
      whenNone: "This style version has no BOM yet.",
      creating: "Creating…",
    },
    add: {
      title: "Add line",
      material: "Material",
      materialPlaceholder: "Select a material",
      qty: "Qty per unit",
      uom: "UoM",
      uomPlaceholder: "Select a UoM",
      waste: "Waste %",
      add: "Add line",
      adding: "Adding…",
    },
    release: {
      release: "Release BOM",
      releasing: "Releasing…",
      confirmTitle: "Release this BOM?",
      confirmBody: "Release is irreversible — a released BOM is frozen and cannot be edited.",
      confirm: "Confirm release",
      cancel: "Cancel",
    },
    perm: {
      createHint: "Creating BOMs requires the Create BOMs permission.",
      editHint: "Adding lines requires the Edit BOMs permission.",
      approveHint: "Releasing requires the Approve BOMs permission.",
    },
    errors: {
      bomExists: "This style version already has a BOM (one per version).",
      noLines: "Add at least one line before releasing.",
      invalidState: "This BOM can no longer be changed.",
      makerChecker: "Maker-checker is on: the creator cannot release their own BOM.",
      revisionConflict: "This BOM was changed by someone else. Reload to see the latest version.",
      materialInvalid: "Unknown material.",
      uomInvalid: "Unknown UoM.",
      qtyPositive: "Quantity per unit must be greater than zero.",
      validationFailed: "Check the highlighted fields.",
      generic: "Something went wrong. Please try again.",
      network: "Web service is not reachable. Please try again.",
      reload: "Reload",
    },
  },
};
