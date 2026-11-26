import { teardown } from "./global-setup";

// Kill the recorded server trees, drop the scratch DB, remove the ledger.
// Runs after the suite — including when tests fail — via Playwright's
// globalTeardown hook.
export default teardown;
