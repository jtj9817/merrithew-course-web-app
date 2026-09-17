# Manual Evidence — Live-App Verification

Manual/live-app evidence for [`TODO.md`](../../TODO.md), kept separate from
automated suite results per the catalog's evidence policy: the
[Phase 7 regression gate](#live-app-smoke-swagger-surfaces) and the
[Phase 9 built-app walkthrough](#phase-9--built-app-walkthrough-no-vite-dev-server).
Automated results live in the
[Phase 7](tdd-plan.md#phase-7-implementation-record) and
[Phase 9](tdd-plan.md#phase-9-implementation-record) implementation records.

Environment: .NET SDK 10.0.112, pnpm 10.33.0 / node v22.22.0, Chrome via
chrome-devtools automation. App run from the Phase 7 commit with
`ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5087`,
`--no-launch-profile`, and an isolated throwaway SQLite file
(`Data Source=/tmp/phase7-inquiries.db`) so seeding started from an empty store.
Frontend assets were rebuilt this session (`pnpm --dir frontend run build`)
before the host started.

## Live-app smoke (Swagger surfaces)

| Check | Result |
| --- | --- |
| `GET /swagger/v1/swagger.json` | `200`, `application/json` |
| `GET /swagger/index.html` | `200` |
| Documented paths | `/`, `/api/inquiries`, `/api/inquiries/{id}`, `/api/inquiries/{id}/status` |
| Startup migration on empty isolated DB | app ready in ~1s, no manual steps |

Seeding (synthetic data only, via the Swagger-documented REST surface):

- 6 × `POST /api/inquiries` → all `201 Created`, ids 1–6, including one
  XSS-sentinel row (`firstName` = `<img src=x onerror=alert(1)>`,
  `courseName` = `<script>alert(2)</script> Total Barre`).
- 4 × `PUT /api/inquiries/{id}/status` → all `200`; final statuses cover all
  five values (New ×2, Contacted, Pending, Registered, Closed).
- `GET /api/inquiries?pageSize=10` → `totalCount: 6`, XSS payloads returned
  verbatim as JSON strings (encoding safe at the API layer).

## MAN-UI-001 — mount, filter, detail, status, reload persistence, XSS inertness

| Step | Result | Evidence |
| --- | --- | --- |
| Island mounts from compiled assets | PASS | `/dashboard` at 1280×800 rendered the table with 6 rows, "6 inquiries", filter/sort selects, pagination "Page 1 of 1" |
| Status filter | PASS | Filter `New` → count updated to 2, only the two `New` rows listed |
| Detail drawer | PASS | "Details for Aiko O'Brien" opened the drawer with all fields (id, names, email, phone, course, location, message, status, created/updated); focus moved into the panel |
| Escape closes drawer + focus return | PASS | After Escape, `document.activeElement` = "Details for Aiko O'Brien" button, drawer removed from DOM |
| Row status update + feedback | PASS | Aiko O'Brien select → "Contacted", Apply clicked: row + select both show Contacted; polite live region announced exactly "Status saved."; assertive region empty |
| Hard-reload persistence | PASS | After cache-bypassed reload, Aiko's row still shows Contacted (server-persisted state) |
| XSS sentinel inert | PASS | Row 5 renders payloads as literal text; DOM probe found zero `<img>` elements and the only `<script>` is the app bundle itself; `window.__xss === undefined`; no dialog appeared at any point |

## MAN-UI-002 — keyboard-only triage

Entire pass executed with keystrokes only (browser-agent session, page reloaded,
focus blurred to BODY first). All PASS:

| Step | Result | Evidence |
| --- | --- | --- |
| Tab order | PASS | Observed: filter select → sort select → per-row (Details → status select → Apply) → next row … → pagination after last row. 3 tab stops per row, no skips |
| Enter opens drawer | PASS | Enter on "Details for Marcus Chen" → panel focused (`aria-label="Inquiry detail"`), all 11 fields present |
| Escape closes + focus return | PASS | Drawer removed from DOM; `document.activeElement` back on the opener button |
| Keyboard status change | PASS | ArrowDown on "Status for Priya Nair" select → Pending; Tab to Apply; Space activated → row shows Pending, live region "Status saved." |
| Console during keyboard pass | PASS | `<no console messages found>` — zero entries all session |

## MAN-UI-003 — JavaScript disabled

Tooling limitation (same as Phase 6): the automation surface cannot disable
JavaScript at the browser level. Evidence substituted:

- JS-less client fetch of `/dashboard` returned the server-rendered shell with
  the loading placeholder (`aria-busy="true"`) and the `<noscript>` guidance
  ("JavaScript is required to use this dashboard…").
- Automated coverage: IT-HOST-003 asserts the same shell contract and passed in
  the Phase 7 non-SQLServer run (146/146).

## MAN-UI-004 — responsive spot checks

Captured by the browser-agent pass (viewport restored to 1280×800 afterwards):

| File | Size | Captured at |
| --- | --- | --- |
| `screenshots/phase7/dashboard-1280.png` | 131,803 B | 1280×800 desktop |
| `screenshots/phase7/dashboard-375.png` | 50,781 B | 375×667 mobile |
| `screenshots/phase7/dashboard-640.png` | 37,598 B | 640×400 (≈200% zoom of 1280) |

Visual review verdicts are recorded in the
[Phase 7 implementation record](tdd-plan.md#phase-7-implementation-record).

## Phase 9 — built-app walkthrough (no Vite dev server)

Recorded 2026-09-17. Executes TODO.md Phase 9 item 1: run the built app without
a Vite dev server; exercise Swagger and triage, keyboard/focus, safe feedback,
and sanitized logs using synthetic data. Automated counterpart:
[Phase 9 implementation record](tdd-plan.md#phase-9-implementation-record).

Environment: same toolchain as Phase 7 (SDK 10.0.112, pnpm 10.33.0 / node
v22.22.0, Chrome via chrome-devtools automation). App run from code revision
`5d5fbe0` with `ASPNETCORE_URLS=http://127.0.0.1:5844
ASPNETCORE_ENVIRONMENT=Development dotnet run --project backend
--no-launch-profile`; full console capture retained at `/tmp/phase9-app.log`
(130 lines). Served `/dashboard` referenced the compiled
`/app/assets/index-Bar7Dhul.js` + `index-CYkj0GM8.css`; grep for `5173` in the
served HTML and the built bundle: 0; no Vite process or listener for this repo.

Seeded synthetic data: 18 new rows via the REST surface over the 5 pre-existing
dev rows — a PII sentinel (id 6: `sentinel.ph9@example.com`, `416-555-0199`),
an XSS probe (id 7: `<img src=x onerror=window.__xss=1>` first name,
`<script>window.__pwn=1</script>` message), and filler rows across all five
statuses (ids 8–23).

### MAN-P9 — Swagger, triage, keyboard, feedback, logs

| Check | Result | Evidence |
| --- | --- | --- |
| Compiled island, no dev server | PASS | network log shows only same-origin `/app/assets/*` (200) + favicon (200); 23 rows rendered, filter/sort/pager present |
| Triage: filter + empty state + paging | PASS | filter `New` → "15 inquiries" all New; filter `Pending` → "0 inquiries" + empty-state text; pager "Page 1 of 2" ↔ "Page 2 of 2" with disabled end controls |
| Detail drawer | PASS | sentinel row drawer shows all fields (id, names, email, phone, course, location, message, status, timestamps) |
| Status update + safe feedback | PASS | id 23 New→Contacted via row select + Apply; polite live region exactly "Status saved." (assertive empty); persisted across cache-bypassed reload (curl-verified) |
| Keyboard/focus | PASS | Tab path: filter → sort → per row (Details → status select → Apply); ArrowDown+Enter status change saved ("Status saved.", id 21 Pending, curl-verified); Enter opens drawer with `activeElement` = panel; Escape closes and focus returns to opener. Caveat: Space-on-select opens a native popup the headless driver cannot operate — ArrowDown+Enter used as the equivalent activation |
| XSS inertness | PASS | probe row renders as escaped literal text; `img[src=x]` absent; `window.__xss`/`window.__pwn` undefined after render, drawer open, filtering, reloads; no dialogs |
| Console clean | PASS | dashboard session: zero console messages (favicon 200); Swagger page shows only a third-party swagger-ui form-field DevTools notice, not app code |
| Swagger manual create/update | PASS | Swagger UI Try-it-out POST → displayed 201 (`id` 25, body echoed); PUT status → 200; GET confirms `Registered`. Tooling note: the CLI's fill first posted swagger-ui's *example* body (id 24, `"string"` fields) — deleted (204) and redone via native value setter |
| Sanitized logs | PASS | sentinel greps on the full capture: `sentinel.ph9@example.com` 0, `sentinel.ph9` 0, `416-555-0199` 0, `Sandra` 0, broader `@example.com`/`416-555`/`Filler` 0; stack traces (`at System.`, `Exception:`) 0. Verbatim CRM line: `CRM sync attempt 1 for inquiry 6 ended with outcome success` (inquiry id/attempt/outcome only). 400/404 probes (missing email → ValidationProblemDetails; unknown id PUT → ProblemDetails) produce no log lines — no request-logging middleware — so no response PII can leak |

Screenshots (this walkthrough):

| File | Captured at |
| --- | --- |
| `screenshots/phase9/dashboard-desktop-1280.png` | 1280×900 desktop, 23 rows |
| `screenshots/phase9/detail-drawer-open.png` | sentinel drawer open, all fields |
| `screenshots/phase9/dashboard-mobile-375.png` | 375×812 @2x (viewport emulation; `resize_page` ineffective in this driver) |

Synthetic seed rows (ids 6–23 and 25) remain in the git-ignored dev SQLite file.
