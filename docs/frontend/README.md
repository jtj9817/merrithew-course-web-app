# Frontend

The dashboard is a **React + TypeScript island** mounted into a server-rendered
Razor shell, not a standalone SPA — one origin, one deployable, no CORS
([ADR-0004](../architecture/adr/0004-razor-shell-react-island.md)). The island owns
list, filter, sort, paging, detail, and status updates; **create and delete are
deliberately API/Swagger operations with no controls here**
([C7](../architecture/contracts.md#c7-web-ui-and-hosting)).

This doc covers the build contract, the state model, and the conventions the code
relies on — not a per-component prop catalogue (the TypeScript types are better at
that).

## Stack

React 19, TypeScript 7, built with **Vite 8**; tested with **Vitest** + jsdom +
Testing Library ([`package.json`](../../frontend/package.json)). No router, no
external state store — the island is a single screen.

```bash
cd frontend
pnpm install
pnpm build      # tsc -b && vite build  -> ../backend/wwwroot/app/
pnpm test       # vitest run
pnpm dev        # optional: Vite dev server (the app itself never requires it)
```

## Build contract (how the shell finds the island)

[`vite.config.ts`](../../frontend/vite.config.ts) pins a contract the backend
depends on: `base: '/app/'`, `build.outDir: '../backend/wwwroot/app'`, and
`manifest: true`. So a build emits hashed assets **and** a
`wwwroot/app/.vite/manifest.json` into a dedicated subfolder (cleaning the build
never deletes unrelated static files).

The Razor side resolves that manifest **per request**
([`ViteManifest`](../../backend/Hosting/ViteManifest.cs)) to find the entry script
and its stylesheets, so no hashed filename is ever hard-coded. A missing or
malformed manifest degrades to the shell's guidance rather than a 500 — which is
exactly the state of a fresh checkout before `pnpm build` runs (the output folder is
git-ignored). The shell ([`Dashboard.cshtml`](../../backend/Pages/Dashboard.cshtml))
renders one mount point `#dashboard-root`, a loading placeholder, and a `<noscript>`
message; [`main.tsx`](../../frontend/src/main.tsx) mounts `App` into that node.

## State model

All state lives in [`App.tsx`](../../frontend/src/App.tsx) via `useState`/`useRef` —
there is no store to mirror it into. The pieces:

- **`request`** (filter + optional page) and **`sort`** drive the list query;
  changing the filter resets to page 1
  ([`listState.ts`](../../frontend/src/lib/listState.ts)).
- **`phase`** is a discriminated union — `loading` | `error` | `ready(envelope)`.
  The **served envelope is authoritative** for the displayed page; client page state
  only drives the *next* request.
- **`detail`** is a `closed` | `loading` | `open` union that also remembers the
  opener element for focus return ([`types.ts`](../../frontend/src/lib/types.ts)).
- **`mutatingIds`** tracks in-flight status updates to disable duplicate submissions.
- **Sequence guards** (`listSeqRef`, `detailSeqRef`): each fetch takes a ticket and
  applies its result only if still current, so a slow older response can't overwrite
  newer state — `AbortController` alone can't undo an already-completed response
  ([C7](../architecture/contracts.md#c7-web-ui-and-hosting)).

Pure, framework-free logic is factored into `src/lib/` and unit-tested in isolation:
[`listQuery`](../../frontend/src/lib/listQuery.ts) (state → same-origin URL),
[`paging`](../../frontend/src/lib/paging.ts) (`computeLastPage`),
[`listState`](../../frontend/src/lib/listState.ts), and
[`format`](../../frontend/src/lib/format.ts). Presentational components live in
`src/components/` (`Toolbar`, `InquiryTable`, `DetailPanel`, `Pagination`,
`LiveRegions`).

## API access

All requests go same-origin through [`lib/api.ts`](../../frontend/src/lib/api.ts)
(`fetchInquiryPage`, `fetchInquiry`, `putInquiryStatus`). Every response — and every
thrown network error — is normalized by
[`lib/fetchOutcome.ts`](../../frontend/src/lib/fetchOutcome.ts) into a small tagged
union: `data` / `record` / `validation` (field keys only, never values) / `gone`
(404) / `problem` (a ProblemDetails title) / `generic`. A non-JSON or unparseable
body degrades to `generic` — a recoverable message, never a parsing crash or raw
HTML. `OUTCOME_MESSAGES` holds the fixed user-facing strings.

## UX conventions the code enforces (C7)

- **Distinguish loading / empty / error**, and offer Retry on a load failure. An
  empty filtered result and an empty store get different messages.
- **Reconcile after a change.** A successful status update patches the row in place,
  announces "Status saved.", then refreshes the list so a row that left the active
  filter disappears and totals reconcile. If the current page empties above page 1,
  the island lands on the last available page and refetches.
- **Honest failure wording.** A write that succeeds but whose refresh fails reads
  *"Status saved, but the list could not be refreshed"* — not "save failed". A `404`
  during detail/update means the record is gone: it clears stale detail and refreshes.
- **Safe rendering.** Visitor and error text render as text (React escaping), so
  markup in a name or message is inert — verified in the browser XSS walkthrough
  ([manual evidence](../testing/manual-evidence.md)).
- **Accessibility** — semantic table + labelled controls, keyboard-operable actions,
  detail-panel focus in/return-to-opener, and polite/assertive live regions. Details
  are in the [accessibility written answer](../../written-answers.md#accessibility).

## Gotchas

- **No create/delete UI by design** — those are API/Swagger operations. The one
  exception is the development-only CRM demonstrator below, which creates a
  single clearly-labelled demo inquiry to exercise the CRM path end to end
  ([C7](../architecture/contracts.md#c7-web-ui-and-hosting)).
- **The dashboard needs the build.** Without `pnpm build`, `/dashboard` shows only
  the shell's "JavaScript required" guidance; the API and Swagger are unaffected.
- **Test-only fetch double.** The Vitest suite installs a FIFO fetch double on both
  `window.fetch` and `globalThis.fetch` (bare `fetch` resolves through Node's global
  in jsdom); it's test infrastructure, not runtime code.

## Development-only tools

Two dashed-border controls render above the toolbar — **only when the Razor
shell confirms their endpoints are mapped** (the shell sets
`window.__scenarioTools` / `window.__crmSimulationTools` from
`DevToolsOptions`, so UI and API surface cannot drift). Both are absent in a
production-shaped host and inert in the test harness: when a flag is off, the
component renders nothing and issues no mount-time request.

- **Scenario switcher** ([`ScenarioSwitcher.tsx`](../../frontend/src/components/ScenarioSwitcher.tsx)):
  seeds or clears the inquiry store through `/api/dev/scenarios` to demo list
  states (empty / full / paging).
- **CRM delivery demonstrator**
  ([`CrmSimulationControl.tsx`](../../frontend/src/components/CrmSimulationControl.tsx)):
  picks a runtime CRM behavior (`Success`, `TransientThenSuccess`,
  `AlwaysTransientFailure`, `PermanentFailure`, `Timeout`,
  `InternalCancellation`) with latency and failures-first knobs, then either
  applies it or runs **one real inquiry** through the production
  `POST /api/inquiries` and reads back only safe CRM metadata
  (`lib/crmSimulation.ts` shape-checks every response). The status line always
  names the outcome and attempt count, and on failure states that the stored
  inquiry is unaffected — the persist-first rule, visible in the UI. Demo
  inquiries appear in the queue like any visitor submission.

Both tools share one refresh callback: after data changes, the island resets to
the default filter/sort view and refetches.

## Related

- [Backend docs](../backend/README.md) · [Boundary contracts](../architecture/contracts.md)
- [Frontend & SQL case catalog](../testing/frontend-and-sql-cases.md) ·
  [manual browser evidence](../testing/manual-evidence.md)
