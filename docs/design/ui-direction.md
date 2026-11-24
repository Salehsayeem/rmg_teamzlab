# UI direction — RMG Operations Platform (synced instruction file)

Source of truth for look + geometry. Kept in lock-step with
`apps/web/src/app/globals.css` — code and this file must never disagree; update
both in the same commit. Token values transcribed from the reference analysis
(`website-analysis/design-tokens.json`, tailwindadmin reference).

## 1. Design tokens (CSS custom properties)

| Token | Light | Dark | Usage |
|---|---|---|---|
| `--background` / `--foreground` | `#ffffff` / `#1c2536` | `#1c2536` / `#ffffff` | page surface / primary text |
| `--card` (+`-foreground`) | `#ffffff` / `#1c2536` | `#1c2536` / `#ffffff` | card surfaces, `p-6` |
| `--popover` (+`-foreground`) | `#ffffff` / `#1c2536` | `#333f55` / `#ffffff` | dropdowns/menus |
| `--muted` | `#f5f5f5` | `#2a3851` | muted fills, skeletons |
| `--muted-foreground` | `rgba(90,106,133,.75)` | `#7c8fac` | reference muted text (see §5 contrast note) |
| `--primary` (+`-foreground`) | `#5d87ff` / `#ffffff` | same | brand: buttons, active nav pill, links, chart-1 |
| `--secondary` (+`-foreground`) | `#49beff` / `#ffffff` | same | brand 2: banners, chart-2 |
| `--success` | `#13deb9` | same | success states, chart-3 |
| `--warning` | `#f6b51e` | same | warning states, chart-4 |
| `--error` / `--destructive` | `#ef4444` | same | destructive/error states, chart-5 |
| `--info` | `#8754ec` | same | info states |
| `--lightprimary` … `--lightinfo` | 10–20% alpha tints: primary 16.5%, secondary/error 20%, success/warning/info 10% | same | stat-tile chips, badge fills |
| `--border` | `#dfe5ef` | `#333f55` | all 1px borders |
| `--input` | `#e5e7eb` | `#333f55` | input borders |
| `--ring` | `#5d87ff` | same | focus rings — never removed |
| `--chart-1..5` | primary/secondary/success/warning/error | same | series colors |
| `--sidebar` (+border) | `#ffffff` / `#dfe5ef` | `#1c2536` / `#333f55` | sidebar surface |
| `--sidebar-foreground` | `#2a3547` | `#7c8fac` | 14px nav/sidebar text; also used for small secondary copy (AA-safe both themes) |
| `--sidebar-primary` (+`-foreground`) | `#5d87ff` / `#ffffff` | same | active nav pill |
| `--radius` | `10px` | same | base radius; `rounded-lg` = 10px applied to cards, controls, pills |

Brand, status, tint, chart and ring tokens are theme-invariant; only surfaces,
text and lines flip. Light/dark = `dark` class on `<html>`, persisted in
localStorage **`rmg-theme`**, seeded pre-paint by an inline script (no flash).

Type: **DM Sans** (`next/font`, variable `--font-dm-sans`) with **Noto Sans
Bengali** fallback (`--font-noto-bengali`) for bn glyphs. Body 14px/20px.

## 2. Geometry constants

| Constant | Value |
|---|---|
| Sidebar width | **268px**, pinned ≥ `xl` (1280px), hidden below (drawer = later task) |
| Header | **72px** tall, sticky top-0, 1px bottom border |
| Controls (inputs/buttons) | **40px** tall (`h-10` / `size-10`) |
| Cards | `p-6` (24px), `rounded-lg` (10px) |
| Grid gutter | `gap-6` (24px); stat tiles `grid-cols-1 sm:2 xl:4` |
| Header search | max-w 320px, leading icon |
| Avatar | 40px circle stub |

## 3. i18n + bidi rules

- Locales: `en`, `bn` only; routes are `app/[lang]/...`; `html lang` + `dir`
  always rendered from the segment (both LTR today, `dir="ltr"` explicit).
- Every UI string keyed in `src/lib/i18n/{en,bn}.ts`; bn implements the exact
  en key set, compile-checked via `Record<Lang, Dict>` in `getDict.ts`.
- No hardcoded UI text in components — shell components receive the dict as props.
- CSS logical properties only (`ps-/pe-`, `ms-/me-`, `start/end`); mixed-script
  data cells get `dir="auto"` (bidi isolation).

## 4. Screen checklist (design gate: light + dark × en + bn)

| Screen | States required | Shell | Status |
|---|---|---|---|
| Dashboard `/[lang]/dashboard` | loading (skeleton), empty (default), error (fixture flag), success (fixture rows) | FullLayout | scaffold done — states behind `FIXTURE_STATE` |
| Login `/[lang]/login` | error banner, 40px controls | BlankLayout | Task 17 |
| Buyers list + form | loading/empty/error/success, conflict inline | FullLayout | later |
| Styles list + detail (variant matrix) | 〃 | FullLayout | later |
| BOM editor (lines grid, approve) | 〃 | FullLayout | later |
| Orders list + capture + confirm | 〃 | FullLayout | later |
| 404 | global not-found (en copy + both-locale links) | none | done (`global-not-found.tsx`) |

## 5. Accessibility floor

- Focus rings never removed (`focus-visible:outline-2 outline-ring offset-2`).
- Text contrast ≥ 4.5:1. Small secondary copy uses `--sidebar-foreground`
  (`#2a3547` on white ≈ 10.9:1; `#7c8fac` on `#1c2536` ≈ 4.7:1). The reference's
  light `--muted-foreground rgba(90,106,133,.75)` measures ≈ 3.3:1 on white —
  kept verbatim as a token for fidelity, but not used for body copy until a
  darker light-mode value is agreed.
- Semantic landmarks + labels: `<nav aria-label>`, `aria-current="page"` active
  pill, `<form role="search">` + `sr-only` label, `aria-label` on icon-only
  controls, `role="alert"`/`role="status"` on error/loading states.
