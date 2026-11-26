import { mkdirSync } from "node:fs";
import path from "node:path";
import { expect, test, type Browser, type Page } from "@playwright/test";
import { apiGet, apiPost, envelope, sessionToken } from "./harness";

/**
 * T23 screenshot sweep: light/dark × en/bn of login, dashboard, buyers, styles,
 * BOM, orders — plus the order capture form, a Draft order detail and the
 * stale-revision conflict banner (review focus #3). Output goes to the plan
 * folder in the MAIN checkout: .superpowers/sdd/01_Plan/t23-screens/.
 *
 * Runs after flow.spec.ts (alphabetical order, workers: 1) so the journey's
 * data exists and the dashboard shows the Confirmed badge. The storage state
 * from the journey's single UI login drives every page here.
 */

const OUT_DIR = path.resolve(__dirname, "..", "..", "..", "..", ".superpowers", "sdd", "01_Plan", "t23-screens");
const STATE_FILE = path.join(__dirname, ".run", "state.json");
const LANGS = ["en", "bn"] as const;
const THEMES = ["light", "dark"] as const;

type Shot = { name: string; path: (lang: string) => string; ready: (page: Page, lang: string) => Promise<void> };

// Ready probes: streaming pages show a role="status" skeleton until their reads land.
const READY: Record<string, (page: Page, lang: string) => Promise<void>> = {
  login: async (page, lang) => {
    await expect(page.getByRole("button", { name: lang === "bn" ? "সাইন ইন" : "Sign in" })).toBeVisible();
  },
  default: async (page) => {
    await expect(page.getByRole("status")).toBeHidden();
  },
};

const SHOTS: Shot[] = [
  { name: "login", path: (l) => `/${l}/login`, ready: READY.login },
  { name: "dashboard", path: (l) => `/${l}/dashboard`, ready: READY.default },
  { name: "buyers", path: (l) => `/${l}/buyers`, ready: READY.default },
  { name: "styles", path: (l) => `/${l}/styles`, ready: READY.default },
  {
    name: "bom",
    path: (l) => `/${l}/styles/${sty001}/bom`,
    ready: async (page, lang) => {
      await expect(page.getByRole("status")).toBeHidden();
      // Heading, not text: the section's aria-label duplicates the word "Lines".
      await expect(page.getByRole("heading", { name: lang === "bn" ? "লাইনসমূহ" : "Lines" })).toBeVisible();
    },
  },
  {
    name: "orders",
    path: (l) => `/${l}/orders`,
    ready: async (page, lang) => {
      await expect(page.getByRole("status")).toBeHidden();
      await expect(page.getByRole("cell", { name: "ORD-001" })).toBeVisible();
      await expect(page.getByText(lang === "bn" ? "অর্ডার" : "Customer orders").first()).toBeVisible();
    },
  },
];

// Seeded demo style (3×4 matrix, released 3-line BOM) — resolved once via the API.
let sty001 = "";

async function snap(browser: Browser, shot: Shot, theme: string, lang: string): Promise<void> {
  const ctx = await browser.newContext({ storageState: STATE_FILE });
  await ctx.addInitScript((t) => localStorage.setItem("rmg-theme", t), theme);
  const page = await ctx.newPage();
  await page.goto(shot.path(lang));
  await shot.ready(page, lang);
  await page.screenshot({ path: path.join(OUT_DIR, `${shot.name}-${theme}-${lang}.png`), fullPage: true });
  await ctx.close();
}

test.describe("screenshots", () => {
  test.beforeAll(async () => {
    mkdirSync(OUT_DIR, { recursive: true });
    const styles = await envelope<{ id: string; code: string }[]>(await apiGet("/pdm/styles"));
    sty001 = styles.find((s) => s.code === "STY-001")!.id;
  });

  for (const shot of SHOTS) {
    test(`sweep: ${shot.name}`, async ({ browser }) => {
      test.setTimeout(180_000);
      for (const theme of THEMES) for (const lang of LANGS) await snap(browser, shot, theme, lang);
    });
  }

  test("order capture form", async ({ browser }) => {
    test.setTimeout(120_000);
    const capture: Shot = {
      name: "order-capture",
      path: (l) => `/${l}/orders/new`,
      ready: async (page, lang) => {
        await expect(page.getByRole("status")).toBeHidden();
        await expect(page.getByRole("heading", { name: lang === "bn" ? "নতুন অর্ডার" : "New order" })).toBeVisible();
      },
    };
    for (const theme of THEMES) {
      await snap(browser, capture, theme, "en");
      await snap(browser, capture, theme, "bn");
    }
  });

  test("order detail draft + stale-revision conflict banner", async ({ browser }) => {
    test.setTimeout(120_000);
    const orders = await envelope<{ id: string; code: string; status: string }[]>(
      await apiGet("/ord/customer-orders?pageSize=100"),
    );
    const draft = orders.find((o) => o.code === "ORD-E2E-03");
    test.skip(!draft, "ORD-E2E-03 comes from flow.spec.ts — run the full suite");

    // Draft detail (confirm affordances visible).
    const detail: Shot = { name: "order-detail-draft", path: (l) => `/${l}/orders/${draft!.id}`, ready: READY.default };
    await snap(browser, detail, "light", "en");
    await snap(browser, detail, "dark", "en");

    // Conflict: this viewer loads the Draft detail, a concurrent confirm moves
    // the revision under it, then the viewer's own Confirm attempt returns 409 —
    // the stale-revision banner renders (T14 pin, review focus #3).
    const ctx = await browser.newContext({ storageState: STATE_FILE });
    await ctx.addInitScript((t) => localStorage.setItem("rmg-theme", t), "light");
    const page = await ctx.newPage();
    await page.goto(`/en/orders/${draft!.id}`);
    await expect(page.getByRole("status")).toBeHidden();
    await apiPost(`/ord/customer-orders/${draft!.id}/confirm`, { expectedRevision: 1 });
    await page.getByRole("button", { name: "Confirm order" }).click();
    await page
      .locator("form")
      .filter({ hasText: "Confirming locks the order at its current revision" })
      .getByRole("button", { name: "Confirm order" })
      .click();
    await expect(page.getByText("This order was updated by another user")).toBeVisible();
    await page.screenshot({ path: path.join(OUT_DIR, "order-detail-conflict-light-en.png"), fullPage: true });
    await ctx.close();
  });
});
