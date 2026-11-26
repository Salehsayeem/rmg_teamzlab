import { defineConfig } from "@playwright/test";

/**
 * Plan-mandated agent verification ports: web 3101 / API 5101. E2E_WEB_URL /
 * E2E_API_URL override them (e.g. a parallel agent's servers squatting the
 * defaults on the shared machine) — the global setup boots both servers on
 * these very URLs, so the override applies end to end.
 */
export const WEB_URL = process.env.E2E_WEB_URL ?? "http://localhost:3101";
export const API_URL = process.env.E2E_API_URL ?? "http://localhost:5101";

export default defineConfig({
  testDir: ".",
  // Serial: the journey builds state step by step (buyer → style → BOM → order)
  // and the login rate limiter allows 10 attempts/min/IP.
  workers: 1,
  retries: 0,
  timeout: 60_000,
  reporter: [["list"]],
  globalSetup: "./global-setup",
  globalTeardown: "./global-teardown",
  use: {
    baseURL: WEB_URL,
    screenshot: "only-on-failure",
    trace: "retain-on-failure",
  },
  projects: [{ name: "chromium", use: { browserName: "chromium" } }],
});
