# Dashboard UI

Nuxt 4 SPA on **shadcn-vue** (`reka-vega` style, zinc base, lucide icons). The shell follows the
`dashboard-01` and `sidebar-07` blocks: an inset sidebar that collapses to icons, a sticky page
header, ⌘K command palette.

## Components

- Primitives live in `app/components/ui/` and are **added with the CLI, never hand-written**:
  `npx shadcn-vue@latest add <name>`. They are auto-imported without a prefix (`<Button>`, `<Card>`).
  The `shadcn-vue` MCP server (repo-root `.mcp.json`) can search the registry and show examples.
- Blocks (`dashboard-01`, `sidebar-07`, …) are published only in the `new-york-v4` style, so they are
  adapted by hand rather than added — `https://shadcn-vue.com/r/styles/new-york-v4/<block>.json`.
- `ui/` files carry a few local changes, each marked `Local to this project`: RTL for the mobile
  sidebar sheet, sidebar tooltips and the switch thumb. Keep them when re-adding a component.
- Inside a file named like a primitive (`media/Card.vue`), import that primitive explicitly — a bare
  `<Card>` is Vue's self-reference.
- App building blocks: `DashboardPage` (header + toolbar slot), `SectionIntro`, `EditorSheet`,
  `ConfirmModal`, `PagerControls`, `LanguagePills`, `SuggestInput`.
- Tables sit in `TableFrame` with `<TableHeader class="sticky-head">`. A table that sorts or hides
  columns uses TanStack Table **v9** (`useTable` + `tableFeatures`, not v8's `useVueTable`) with
  `SortableHead` for its headers and `TablePager` for its footer — see `pages/audit.vue` and
  `pages/catalog.vue`. The installed package ships its own
  guidance under `node_modules/@tanstack/{vue-table,table-core}/skills/`.

## Rules

- Colours only through theme tokens (`bg-background`, `text-muted-foreground`, `border`,
  `bg-primary/10`, `text-success`, `text-warning`) — never raw palette classes like `bg-zinc-100`.
  Tokens and the accent presets are in `app/assets/css/main.css`.
- One font everywhere: Geist, with Tajawal taking the Arabic glyphs. Identifiers (SKUs, codes,
  GUIDs) are set in it too — no `font-mono`; `--font-mono` points at the same stack.
- Icons are `@lucide/vue` components. GitHub's mark is `GithubIcon.vue` (lucide has no brands).
- Every visible string goes through `$t(...)`, with the key added to all three of
  `i18n/locales/{en,ar,fr}.json`. Identifiers (codes, roles, SKUs, routes) are not translated and
  get `dir="ltr"`; user-entered values get `dir="auto"`.
- Arabic is RTL: logical utilities only (`ms-*`, `pe-*`, `start-*`, `text-start`). A sheet's `side`
  is physical — pick it from `useShellLocale().direction`. Mirror directional icons with
  `rtl:rotate-180`.
- Toasts go through `useNotify()` (vue-sonner underneath).
- Buttons have no `loading` prop: disable them and put `<Spinner />` first.

## Checks

`npm run lint`, `npm run typecheck`, `npm test`, `npm run test:e2e` — all four run in CI. The last is
Playwright against the dev server with every `/api/**` call stubbed in `test/e2e/api.ts`: a page that
calls a new endpoint needs a stub there, and a new dependency imported from a page needs a line in
`vite.optimizeDeps.include`. Before calling UI work done, open it in the browser pane and look at it
in light and dark, in English and Arabic, and at 375px.
