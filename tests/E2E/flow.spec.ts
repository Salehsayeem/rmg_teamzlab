import { expect, test, type Page } from "@playwright/test";
import { apiGet, apiPost, envelope, expectOk, STATE_FILE } from "./harness";

/**
 * T23 full pilot journey against the scratch DB (fresh per run, spec §8 demo
 * dataset seeded): login → create buyer → style 2×2 → approve → BOM line →
 * release → order capture → confirm → dashboard shows Confirmed — plus the bn
 * locale probe (Bangla labels with Latin codes, review focus #4). Serial by
 * design: each step feeds the next; the first UI login saves the storage state
 * every later step reuses (one login run-wide — the limiter allows 10/min/IP).
 */

const ADMIN = { email: "admin@tzl.demo", password: "Admin#12345" };
const BUYER = { code: "BUY-E2E-01", name: "E2E Buyer Ltd", country: "BD", currency: "USD" };
const STYLE = { code: "STY-E2E-01", name: "E2E Polo Shirt", colors: ["RED", "BLUE"], sizes: ["S", "M"] };
const ORDER = { code: "ORD-E2E-01", po: "PO-E2E-01", currency: "USD", date: "2026-12-01", ship: "2027-01-15" };

// State carried down the serial chain (workers: 1, declaration order).
let buyerId = "";
let styleId = "";

async function idFromUrl(page: Page): Promise<string> {
  await page.waitForURL(/\/[a-z]{2}\/[^/]+\/[0-9a-f-]{36}$/);
  return new URL(page.url()).pathname.split("/").pop()!;
}

test.describe("pilot journey", () => {
  test.describe.serial("signed-in journey", () => {
    test("login via UI reaches the dashboard", async ({ page }) => {
      await page.goto("/en/login");
      await page.getByLabel("Email").fill(ADMIN.email);
      await page.getByLabel("Password").fill(ADMIN.password);
      await page.getByRole("button", { name: "Sign in" }).click();

      await page.waitForURL("**/en/dashboard");
      // Streaming tiles settle: the skeleton goes away once the reads land.
      await expect(page.getByRole("status")).toBeHidden();
      await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
      await expect(page.getByRole("heading", { name: "Recent orders" })).toBeVisible();
      // The seeded demo order is visible right after login.
      await expect(page.getByRole("cell", { name: "ORD-001" })).toBeVisible();

      await page.context().storageState({ path: STATE_FILE });
    });

    // Everything after login rides the session the first test saved (one UI
    // login per run — the login limiter allows 10 attempts/min/IP).
    test.describe("as admin", () => {
      test.use({ storageState: STATE_FILE });

      test("create buyer", async ({ page }) => {
        await page.goto("/en/buyers/new");
        // Element ids over label text: the contacts section repeats a "Name" label.
        await page.locator("#buyer-code").fill(BUYER.code);
        await page.locator("#buyer-name").fill(BUYER.name);
        await page.locator("#buyer-country").fill(BUYER.country);
        await page.locator("#buyer-currency").fill(BUYER.currency);
        await page.getByRole("button", { name: "Create buyer" }).click();

        buyerId = await idFromUrl(page);
        // The code shows twice on the detail page (subtitle + facts definition).
        await expect(page.getByText(BUYER.code).first()).toBeVisible();
      });

      test("create style 2×2 and approve", async ({ page }) => {
        await page.goto("/en/styles/new");
        await page.locator("#style-code").fill(STYLE.code);
        await page.locator("#style-name").fill(STYLE.name);
        await page.locator("#style-buyer").selectOption(buyerId);
        for (const color of STYLE.colors) {
          await page.getByLabel("Color codes").fill(color);
          await page.getByLabel("Color codes").press("Enter");
        }
        for (const size of STYLE.sizes) {
          await page.getByLabel("Size codes").fill(size);
          await page.getByLabel("Size codes").press("Enter");
        }
        await expect(page.getByText("4 variants")).toBeVisible();
        await page.getByRole("button", { name: "Create style" }).click();

        styleId = await idFromUrl(page);
        await expect(page.getByText(STYLE.code).first()).toBeVisible();

        await page.getByRole("button", { name: "Approve" }).click();
        await expect(page.getByText("Approved").first()).toBeVisible();
      });

      test("create BOM, add line, release", async ({ page }) => {
        await page.goto(`/en/styles/${styleId}/bom`);
        await page.getByRole("button", { name: "Create BOM" }).click();
        // createBom redirects back here with the Draft BOM + add-line panel rendered.
        await expect(page.getByRole("heading", { name: "Add line" })).toBeVisible({ timeout: 15_000 });
        await expect(page.getByText("Draft").first()).toBeVisible();

        // The demo material set is seeded: pick the fabric, KG as its base uom.
        await page.locator("#bom-material").selectOption({ label: "Cotton single jersey 180 gsm (RM-FAB-01)" });
        await page.locator("#bom-qty").fill("1.5");
        await page.locator("#bom-uom").selectOption({ label: "Kilogram (KG)" });
        await page.getByRole("button", { name: "Add line", exact: true }).click();
        await expect(page.getByRole("cell", { name: "Cotton single jersey 180 gsm" })).toBeVisible();

        await page.getByRole("button", { name: "Release BOM" }).click();
        await page.getByRole("button", { name: "Confirm release" }).click();
        await expect(page.getByText("Released").first()).toBeVisible();
      });

      test("capture order via the orders UI", async ({ page }) => {
        await page.goto("/en/orders/new");
        await page.locator("#order-code").fill(ORDER.code);
        await page.locator("#order-buyer").selectOption(buyerId);
        await page.locator("#order-po").fill(ORDER.po);
        await page.locator("#order-currency").fill(ORDER.currency);
        await page.locator("#order-date").fill(ORDER.date);
        await page.locator("#order-line-0-version").selectOption({ label: `${STYLE.code} · v1` });
        await page.locator("#order-line-0-price").fill("5.5");
        for (const color of STYLE.colors)
          for (const size of STYLE.sizes)
            await page.getByLabel(`${color} / ${size} Qty`).fill("100");
        await page.getByLabel("Line 1 Ship date 1").fill(ORDER.ship);
        await page.getByLabel("Line 1 Qty 1").fill("400");
        await page.getByRole("button", { name: "Capture order" }).click();

        await page.waitForURL(`**/en/orders/*`);
        await expect(page.getByText(ORDER.code).first()).toBeVisible();
        await expect(page.getByText("Draft").first()).toBeVisible();
      });

      test("confirm order and see Confirmed", async ({ page }) => {
        await page.goto("/en/orders");
        await page.getByRole("row").filter({ hasText: ORDER.code }).getByRole("link").click();

        await page.getByRole("button", { name: "Confirm order" }).click();
        await page
          .locator("form")
          .filter({ hasText: "Confirming locks the order at its current revision" })
          .getByRole("button", { name: "Confirm order" })
          .click();

        await page.waitForURL("**/en/orders/*");
        await expect(page.getByText("Confirmed").first()).toBeVisible();
      });

      test("dashboard shows the confirmed order", async ({ page }) => {
        await page.goto("/en/dashboard");
        await expect(page.getByRole("status")).toBeHidden();
        const row = page.getByRole("row").filter({ hasText: ORDER.code });
        await expect(row).toBeVisible();
        await expect(row.getByText("Confirmed")).toBeVisible();
        await expect(page.getByRole("cell", { name: "ORD-001" })).toBeVisible();
      });

      test("bn dashboard renders Bangla labels with Latin business codes", async ({ page }) => {
        await page.goto("/bn/dashboard");
        await expect(page.getByRole("status")).toBeHidden();
        await expect(page.getByRole("heading", { name: "ড্যাশবোর্ড" })).toBeVisible();
        await expect(page.getByRole("heading", { name: "সাম্প্রতিক অর্ডার" })).toBeVisible();
        // Latin codes stay Latin inside the bn layout (review focus #4).
        await expect(page.getByRole("cell", { name: "ORD-001" })).toBeVisible();
        await expect(page.getByRole("row").filter({ hasText: ORDER.code })).toBeVisible();
      });

      // Negative probe on the orders route: a junk id must surface a 404 page,
      // never a 500 (cross-tenant junk behaves as missing). Asserts the app's
      // [lang]/not-found.tsx boundary: localized copy per segment locale.
      test("unknown order id renders localized not-found", async ({ page }) => {
        await page.goto("/en/orders/00000000-0000-0000-0000-000000000000");
        await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
        await page.goto("/bn/orders/00000000-0000-0000-0000-000000000000");
        await expect(page.getByRole("heading", { name: "পৃষ্ঠাটি পাওয়া যায়নি" })).toBeVisible();
      });
    });
  });
});

// API-level pins that complement the UI journey (same scratch DB): the W4
// capture/confirm contract — stale revision conflicts and idempotent replay.
// Ids resolve from the API (not the journey's module state) so the pins stand
// alone even if a worker restart wipes in-memory state.
test.describe("orders API pins", () => {
  async function e2eRefs(): Promise<{ buyerId: string; versionId: string }> {
    const [buyers, styles] = await Promise.all([
      envelope<{ code: string; id: string }[]>(await apiGet("/ord/buyers?pageSize=100")),
      envelope<{ code: string; versions: { id: string; status: string }[] }[]>(await apiGet("/pdm/styles")),
    ]);
    return {
      buyerId: buyers.find((b) => b.code === BUYER.code)!.id,
      versionId: styles.find((s) => s.code === STYLE.code)!.versions[0].id,
    };
  }

  test("confirm replay with the same idempotency key does not double-write", async () => {
    const { buyerId, versionId } = await e2eRefs();

    const res = await apiPost("/ord/customer-orders", {
      code: "ORD-E2E-02",
      buyerId,
      buyerPo: "PO-E2E-02",
      currency: "USD",
      orderDate: "2026-12-20",
      lines: [{ styleVersionId: versionId, price: 4, sizes: [{ colorCode: "RED", sizeCode: "S", qty: 10 }] }],
    });
    await expectOk(res);
    const created = (await (res.json() as Promise<{ data: { id: string; status: string } }>)).data;
    expect(created.status).toBe("Draft");

    const key = crypto.randomUUID();
    const confirm = (id: string) => apiPost(`/ord/customer-orders/${id}/confirm`, { expectedRevision: 1 }, key);

    const first = await confirm(created.id);
    await expectOk(first);
    const firstBody = (await (first.json() as Promise<{ data: { status: string; revision: number } }>)).data;
    expect(firstBody.status).toBe("Confirmed");

    const replay = await confirm(created.id);
    const replayBody = (await (replay.json() as Promise<{ data: { status: string; revision: number } }>)).data;
    expect(replay.status).toBe(first.status);
    expect(replayBody).toEqual(firstBody);
  });

  test("stale revision confirm conflicts with 409", async () => {
    const { buyerId, versionId } = await e2eRefs();
    const res = await apiPost("/ord/customer-orders", {
      code: "ORD-E2E-03",
      buyerId,
      buyerPo: "PO-E2E-03",
      currency: "USD",
      orderDate: "2026-12-21",
      lines: [{ styleVersionId: versionId, price: 4, sizes: [{ colorCode: "BLUE", sizeCode: "M", qty: 5 }] }],
    });
    const created = (await (res.json() as Promise<{ data: { id: string } }>)).data;

    const stale = await apiPost(`/ord/customer-orders/${created.id}/confirm`, { expectedRevision: 99 });
    expect(stale.status).toBe(409);
    const problem = (await stale.json()) as { code?: string };
    expect(problem.code).toBe("ORDER_REVISION_CONFLICT");
  });
});
