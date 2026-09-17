# Manual Evidence — Phase 7 Regression Gate

Recorded 2026-09-17. Manual/live-app evidence for Phase 7 of
[`TODO.md`](../../TODO.md), kept separate from automated suite results per the
catalog's evidence policy. Automated results live in the
[Phase 7 implementation record](tdd-plan.md#phase-7-implementation-record).

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
