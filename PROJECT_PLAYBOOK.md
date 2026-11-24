# PROJECT PLAYBOOK — standing instructions & commands

> Compiled 2026-11-24 from the owner's standing instructions. Generic by design:
> the same commands drive any project built from scratch. Project-specific names
> appear as placeholders — fill them once per project, keep everything else as-is.
> Save this file at the repo root of every project and keep it current.

---

## 1. Commands the owner uses (and exactly what they trigger)

| Owner says | Meaning |
|---|---|
| `fan out subagents` | Launch implementer agents in parallel where file-safe; controller stays inline on integration + shared files. One agent per independent domain; shared-file domains run serially or rebase at merge. |
| `go` / `continue` / `complete the remaining tasks` | Drive all queued work to completion without check-ins: land → review → fix rounds → merge → full regression → push → default branch. |
| `pause <area>` / `resume` | Freeze that track immediately (committed baseline stays); resume on the explicit word. Other tracks keep moving. |
| `project run koro` / `run the project` | Boot the app on the standard ports for the owner; report URLs + credentials. |
| `<path>` + `complete the design according to this` | A design reference was provided: study it, extract its tokens/components, re-plan, re-sync the instruction file, re-theme. |
| `keep the instruction file in sync` | Maintain one instruction file per concern (design → `docs/design/ui-direction.md`; process → this playbook). Code and file must never disagree; update both in the same commit. |
| `save all instructions in one file, generic` | Compile every standing rule into this playbook so it is reusable across projects. |
| `all terminal responses in English` | Owner-visible replies in English regardless of the language of the owner's message. |

## 2. Branch, commit, and merge discipline

```
default branch (main)   ← production-ready snapshots only
integration branch      ← long-running working branch; FULL regression gate
feat/… fix/… chore/…    ← one feature or bug per branch; tests green on the branch first
```

- Flow: feature branch → tests green on it → merge into the integration branch → **full
  regression** (backend suite + frontend build + live endpoint smoke) → all green → merge
  into the default branch. Never skip the regression gate.
- Conventional Commits (`feat:` `fix:` `chore:` `test:` `docs:` `refactor:`) + the assistant
  attribution line the owner configured.
- Worktrees live **inside the repo** at `.worktrees/` (gitignored) — never as sibling
  directories outside.
- Multi-slice merges touching a shared schema/model file: union-resolve (keep the SUPERSET
  of any seed/permission rows), then regenerate the generated-mirror file from the merged
  model using a throwaway probe (add → delete), keeping only the regenerated mirror. Verify
  the final artifact by probe/build, never by assuming.

## 3. Agent orchestration (fan-out pattern)

- **Controller (inline)** owns: integration, merges, conflict resolution, the plan/ledger
  files, and the critical path. **Implementers** own one independent domain each, in their
  own worktree, on their own branch, cut from the current integration head.
- Every implementer dispatch carries: the brief (spec + binding rulings), the structure
  contract, reference files to read, hard rules (TDD, no new dependencies, scoping), the
  test/verification bar, commit format, a report path, and "DONE_WITH_CONCERNS is an honest
  valid outcome".
- Every landed slice gets a **fresh-context reviewer** (strongest available model,
  read-only) on a diff package. Reviewer output → re-grade by user impact → Important
  findings enter ONE fix round (each fix verified by a test that failed first); Minors are
  ledgered as deferred, never silently dropped.
- Reports and the running ledger live in one workspace directory per plan
  (`.superpowers/sdd/<plan>/`): briefs, reports, review packages, screenshots, probes.
- Agents never touch the main tree's checked-out files, other branches, lock files, or each
  other's worktrees.

## 4. Quality gates (non-negotiable)

- **TDD**: failing test written first and watched failing, then minimal code to green.
  Suite fully green before any commit lands. New endpoints get tests incl. negative paths.
- **Layered architecture**: Controller → Service → Repository; validation before business
  logic; business logic never in controllers.
- **One response envelope** for success and error, everywhere.
- **Security**: authN + permission check on every endpoint; tenant/entity scoping on every
  query; parameterized queries only; secrets in gitignored `.env`; never echo hashes/tokens.
- **Money/state integrity**: idempotency on creates (reserve + complete in one transaction);
  maker-checker (preparer ≠ approver) on approvals; a shared guard on every state-changing
  posting path; status transitions guarded at the write, not just in memory.
- **Forward-only migrations**; never edit an applied one; if a database already applied the
  old shape, fix forward with a new migration and verify the live schema with a read-only
  probe. New tables get tenant/entity scoping from day one.
- **Plan file** (`docs/NN_Plan.md`): living checklist updated after every completed step,
  with a done-log table (date / step / evidence) and a pause/resume banner.

## 5. Design workflow (when the owner provides a reference)

1. Look at the reference in a real browser; extract its tokens (palette, type, radius,
   shadows, dark/light mechanism) — from an analysis JSON if provided, else by inspection.
2. Re-plan: token table → shell layout → component inventory → screen checklist.
3. Re-sync the instruction file (`docs/design/ui-direction.md`) BEFORE or WITH the code.
4. Re-theme the foundation (global stylesheet + fonts + theme toggle), then the shell,
   then screens; existing class names stay stable so screens keep working during the pass.
5. Verify in-browser: every key screen in light AND dark, desktop AND mobile; screenshots
   reviewed before commit. Accessibility floor: contrast ≥ 4.5:1 text, focus rings never
   removed, four states (loading/empty/error/success) on every screen, RTL-safe properties.

## 6. Ops rules

- The owner owns the standard dev ports and terminals; agent verification boots use an
  alternate port and are **stopped after use**. A background chain must never end with a
  long-running server (it never exits → no completion notification); boots are separate.
- An applied-then-edited migration is inert on databases that already applied it — probe
  the live schema, don't assume.
- Background agents die on network storms without losing their transcripts; resume them
  from where they stopped rather than restarting.
- Keep one workspace ledger with dated entries: dispatches, verdicts, rulings
  (`R-<series>: <decision> — <cost if wrong>`), incidents, and deferred minors. The ledger
  is the memory that survives compaction.

## 7. Build-from-scratch sequence (proven order)

1. **Decisions** — record open decisions with recommended defaults; defer what the phase
   doesn't need.
2. **Foundation** — scaffold app + database + auth stub + envelope + error middleware +
   i18n scaffold + audit/outbox + seed data. Gate: builds clean.
3. **Pilot slice** — one end-to-end money path through every layer, with negative-path
   tests. Gate: suite green, flows browser-verifiable.
4. **Deep slices per domain** — brief (spec cards, models, permissions, endpoints,
   journals, tests, ambiguities → controller rulings) → implementer → review → fix round.
5. **Live verification** — scripted smoke of every endpoint against the real database.
6. **Merge wave + full regression → default branch.**
7. **UI pass** — design reference → instruction file → foundation → shell → kit → screens.
8. **Docs pass** — run guide, troubleshooting table, architecture summary, this playbook.

## 8. When something is ambiguous

Ask when a decision is genuinely the owner's (irreversible, costly to reverse, or a real
product fork). Otherwise: rule it, record it in the ledger as a numbered ruling with its
cost-if-wrong, and keep moving. Silence in the spec + a binding ruling beats a stalled build.
