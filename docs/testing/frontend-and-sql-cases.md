# Frontend and SQL test cases — Course Inquiry Dashboard

> **Status: implemented and verified.** The frontend, host, browser, and SQL
> Server cases in this catalog have passing evidence in the
> [Phase 9 implementation record](tdd-plan.md#phase-9-implementation-record) and
> [manual evidence](manual-evidence.md). This catalog refines the target boundary
> contracts ([contracts.md](../architecture/contracts.md), C1–C8) and
> [ADR-0009](../architecture/adr/0009-testable-boundary-contracts.md) into
> executable case tables. Requirement/verification identifiers and evidence live
> in [model.json](../architecture/model.json).

## 1. Scope

This file owns:

- **UT-UI-###** — focused pure client behavior (only where the implementation
  naturally factors a pure function; no abstraction created solely for tests).
- **IT-UI-###** — real React component tree integration under jsdom with a
  controlled fetch boundary.
- **IT-HOST-###** — Razor shell + compiled production asset integration
  (real build output, real HTTP requests, no Vite dev server).
- **IT-SQL-###** — SQL Server script and exact report query integration
  (actual `database/database.sql` against a real disposable SQL Server).
- **MAN-UI-###** — supplemental real-browser walkthroughs (not substitutes for
  automated cases).

Out of scope (owned by the backend case catalog and the parent plan): API,
service, CRM, EF/SQLite persistence, startup-migration, and cancellation/timeout
cases. The UI has **no create/delete feature** (C7, ADR-0009); create/delete are
API/Swagger operations and are tested only as such.

## 2. Test stacks and lanes

### 2.1 Frontend unit and component lanes (UT-UI, IT-UI)

- **Stack:** Vitest + jsdom + React Testing Library + `user-event`.
- **Location/script:** test files live at `frontend/src/**/*.test.ts(x)` and run
  via the frontend package script `test` → `vitest run`. They are not part of
  the `dotnet test` filter. No separate frontend test project is introduced.
  Each Vitest `describe`/test name includes its case ID (e.g.
  `describe('IT-UI-010 …')`), per [tdd-plan.md](tdd-plan.md).
- **Controlled fetch boundary:** tests install a controllable `fetch` double on
  `window` (jsdom provides no fetch). The double serves **canned responses
  built verbatim from the canonical contracts in C1–C4** — never invented
  shapes. Assertions may inspect requests the component *issued* (URL, method,
  headers, JSON body) and the rendered DOM; tests **never** assert the double's
  echo, never snapshot the DOM, and never assert implementation wiring.
- **Controlled time:** fixtures use fixed ISO-8601 UTC instants; timers use
  Vitest fake timers (`vi.useFakeTimers()`). All in-flight responses are
  resolved manually via deferred promises in a deterministic order. **No real
  sleeps, no `setTimeout`-based retries, no timing races.**
- These are **simulated-network tests**: they prove component logic against
  canonical payloads. They do **not** prove hosting, bundling, asset emission,
  or real-browser behavior — IT-HOST and MAN-UI do.

### 2.2 Host integration lane (IT-HOST)

- **Stack:** xUnit in the single test project
  `tests/CourseInquiryDashboard.Tests` (per [tdd-plan.md](tdd-plan.md)),
  directory `Integration/`, trait `Category=Integration`, plus a `CaseId` trait
  per case (e.g. `CaseId=IT-HOST-002`). Uses `WebApplicationFactory`/in-process
  server + `HttpClient`; **no browser** (MAN-UI covers browsers).
- **Prerequisite:** the **production frontend build** has been produced
  (`pnpm --dir frontend build` → emitted into the dedicated `backend/wwwroot`
  asset subfolder plus the Vite manifest). The lane fixture builds once if the
  manifest is missing. The Vite dev server is **never** started or required.
- **Pinned build contract for tests** (a build-configuration decision, not a
  business rule): assets emit under `backend/wwwroot/app/`, public base `/app/`,
  manifest at `backend/wwwroot/app/.vite/manifest.json`. If implementation pins
  a different folder, update the constant in the fixture — the cases below
  reference "the manifest-emitted subfolder", not a hardcoded hash.

### 2.3 SQL Server lane (IT-SQL)

- **Stack:** xUnit in the same single project, directory `SqlServer/`, trait
  `Category=SqlServer`, plus `CaseId` traits. Uses `Microsoft.Data.SqlClient`
  (or `sqlcmd` for GO-batch execution) — no EF, no SQLite substitute, no
  reimplementation of query logic.
- **Lane policy:** excluded from the default local run
  (`dotnet test --filter 'Category!=SqlServer'`); **required in the release
  pipeline**. Connection comes from the `SQLSERVER_TEST_CONNECTION_STRING`
  environment variable pointing at a **disposable** SQL Server (typically a CI
  service container). **No committed secrets.**
- **Blocked ≠ green:** if no SQL Server is reachable or the variable is unset,
  every IT-SQL case reports **Blocked** with the reason (missing infrastructure,
  missing build artifact, missing `database.sql`). Blocked evidence is never
  counted as passing, locally or in release. Missing the script/queries is a
  **failure** of the script contract, not a skip.
- Setup/teardown and safety rules are in §8.

### 2.4 Manual browser walkthroughs (MAN-UI)

Performed against the real running app (production build, `dotnet run`) in a
real browser. Evidence = screenshots/notes; no permanent Playwright suite is
required. **Supplemental only** — they never replace automated cases above.

## 3. Conventions

- **Case tables.** Columns: *Case ID*, *Arrange / input*, *Act*, *Observable
  result*, *Traceability*. One behavior per case; no Cartesian padding.
- **Traceability.** `REQ-*` / `VER-*` identifiers come from `model.json`;
  `C1`–`C8` refer to the sections of
  [contracts.md](../architecture/contracts.md), which owns the normative rules
  (field table, status rules, pagination shape, UI contract, SQL semantics).
  This file does not duplicate policy tables. All identifiers cited here exist
  in `model.json`.
- **Pinned UI interaction contract for executability.** Unless a case says
  otherwise, the island renders: a status filter `<select>` labeled
  "Filter by status" with options *All statuses, New, Contacted, Pending,
  Registered, Closed*; an inquiries `<table>` with column headers (First name,
  Last name, Course, Status, Created) where each row has a details opener
  `<button>`; a row status `<select>` plus "Apply" `<button>` per row (changing
  the select alone sends nothing; Apply issues the PUT); a single detail panel;
  one polite live region (`role="status"`) and one assertive live region
  (`role="alert"`); dates rendered as ISO date `YYYY-MM-DD` in UTC. Exact copy
  is free; cases pin only matchable regexes.
- **No fake success.** Catalogs describe intended behavior; nothing here has
  been executed. Model verification statuses stay `planned` until tests land.
- **Safety.** All fixtures are synthetic; no real personal data is used in any
  lane or walkthrough (C7).

## 4. Shared frontend fixtures

Canonical wire shapes (C1) used verbatim by the fetch double.

**FIX-INQ-1** (`GET /api/inquiries` item; `GET /api/inquiries/1` body):

```json
{
  "id": 1,
  "firstName": "Avery",
  "lastName": "O'Neill",
  "email": "avery.oneill@example.com",
  "phone": "+1 555 0100",
  "courseName": "Yoga Teacher Training — Fall Cohort",
  "preferredLocation": "Calgary NW",
  "message": "Please send the syllabus & pricing.",
  "status": "New",
  "createdDate": "2026-09-10T14:00:00Z",
  "updatedDate": "2026-09-10T14:00:00Z"
}
```

**FIX-INQ-2** (nullable fields null):

```json
{
  "id": 2,
  "firstName": "Céline",
  "lastName": "Fernández",
  "email": "celine.fernandez@example.com",
  "phone": null,
  "courseName": "Barre Fundamentals",
  "preferredLocation": null,
  "message": null,
  "status": "Contacted",
  "createdDate": "2026-09-09T09:30:00Z",
  "updatedDate": "2026-09-12T16:45:00Z"
}
```

**FIX-INQ-XSS** — FIX-INQ-1 with `firstName` replaced by
`Ada <img src=x onerror="window.__xss=true">` and `message` replaced by
`<script>window.__xss=true</script> & "quotes" 'apostrophes'`.

**Envelopes (C4):**

```json
{ "items": [FIX-INQ-1, FIX-INQ-2], "page": 1, "pageSize": 20, "totalCount": 2 }
```

```json
{ "items": [], "page": 1, "pageSize": 20, "totalCount": 0 }
```

Multi-row scenarios seed additional synthetic rows procedurally (e.g. "ids 1–45
with `createdDate` ascending by id") served by the double's canned envelopes;
rows are located via accessible roles, e.g. the FIX-INQ-1 row is
`getByRole('row', { name: /O'Neill/ })`.

**Problem details (C3; tests never pin `title` wording, `type` URL, or property
order):**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "status": ["The status field is required."] }
}
```

```json
{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5", "title": "Not Found", "status": 404 }
```

```json
{ "title": "An unexpected error occurred.", "status": 500 }
```

**PUT success (C2, C7):** request body is exactly `{"status":"Contacted"}`;
response is the full FIX-INQ-1 shape with `"status": "Contacted"` and
`"updatedDate": "2026-09-14T10:15:00Z"` (`createdDate` unchanged). For the
same-status no-op (IT-UI-018) the response returns the row **unchanged**
(`updatedDate` still `2026-09-10T14:00:00Z`).

## 5. Pure-unit cases (UT-UI)

Apply **only** to functions the implementation factors naturally (query
serialization, page math, fetch-outcome classification). If the implementation
does not factor such a function, the behavior is still covered by the IT-UI
case noted; **no helper module may be created solely to earn a unit test**
(ADR-0009).

| Case ID | Arrange / input | Act | Observable result | Traceability |
| --- | --- | --- | --- | --- |
| UT-UI-001 | List-state object `{}` (no filter, page 1, default page size, default sort) | Call the list-query serializer | Produces `/api/inquiries` with **no** `status`, `page`, `pageSize`, or `sort` parameter — omitted values mean server defaults (C4); the empty-string filter never serializes as `status=` (invalid per C2) | REQ-UI-001; VER-UI-002; C2, C4 |
| UT-UI-002 | `{ status: 'Contacted', page: 3, pageSize: 50, sort: 'createdDateAsc' }` | Call the list-query serializer | Parses (via `URLSearchParams`) to exactly `status=Contacted`, `page=3`, `pageSize=50`, `sort=createdDateAsc`; status uses canonical casing from the fixed option set; only `createdDateDesc`/`createdDateAsc` are ever emitted (exact spelling, C4). Behavior additionally observable end-to-end in IT-UI-005/006/008 | REQ-UI-001; VER-UI-002; C2, C4 |
| UT-UI-003 | State `{ filter: 'New', page: 3 }` | Apply filter change to `'Closed'`; separately apply page change to `2` | Filter change yields `{ filter: 'Closed', page: 1 }` (changing a filter resets page to 1, C7); page change yields `{ filter: 'Closed', page: 2 }` (paging preserves the filter). Behavior observable end-to-end in IT-UI-005/007 | REQ-UI-001; VER-UI-002; C7 |
| UT-UI-004 | Inputs `(page, totalCount, pageSize)`: `(3, 41, 20)`, `(2, 0, 20)`, `(1, 0, 20)`, `(3, 40, 20)` | Call the last-available-page calculator | Returns `3`, `1`, `1`, `2` respectively: `max(1, ceil(totalCount / pageSize))` — never `0`, never above the last page; current page kept when still valid. (Corrected 2026-09-17: the first tuple originally read `2`, contradicting the normative ceil formula — 41 items at 20/page span three pages; the implementation record holds the red evidence.) Behavior observable end-to-end in IT-UI-009/017 | REQ-UI-001; VER-UI-002; C7 |
| UT-UI-005 | Fetch outcomes: (a) 200 envelope; (b) 400 `ValidationProblemDetails` with `errors.status`; (c) 404 `ProblemDetails`; (d) 500 `ProblemDetails`; (e) 502 with `text/html` body `<html>…</html>`; (f) fetch rejection `TypeError` | Call the fetch-outcome classifier | Returns exactly: (a) `{ kind: 'data', envelope }`; (b) `{ kind: 'validation', fields: ['status'] }` (field keys surfaced, values never echoed, C3); (c) `{ kind: 'gone' }`; (d) `{ kind: 'problem', title: 'An unexpected error occurred.' }`; (e) `{ kind: 'generic' }` — raw HTML body is never exposed; (f) `{ kind: 'generic' }` — network failures get the recoverable fallback message. Each kind maps to a fixed message variant consumed by IT-UI cases | REQ-UI-001; REQ-SYS-005; VER-UI-002; C3, C7 |

## 6. Component-integration cases (IT-UI)

Real React tree, jsdom, controlled fetch double (§2.1). Requests are asserted
only as the component issued them; results are asserted on the rendered DOM and
live regions.

### 6.A Initial fetch and distinct states

| Case ID | Arrange / input | Act | Observable result | Traceability |
| --- | --- | --- | --- | --- |
| IT-UI-001 | Fetch double programmed: first list call pending; then canned 200 envelope `[FIX-INQ-1, FIX-INQ-2]`, `totalCount: 2` | Mount the island | (1) While pending: loading indicator visible, no rows rendered, no error text. (2) After resolve: table shows 2 rows in array order (O'Neill before Fernández); row renders first name, last name, course, canonical status text (`New`/`Contacted`), created date `2026-09-10`; envelope-derived total "2" shown. `GET /api/inquiries` was requested with no `status`/`page`/`pageSize`/`sort` params. Table exposes `columnheader` roles | REQ-UI-001; VER-UI-001; C1, C4, C7 |
| IT-UI-002 | Fetch double returns 200 `{ items: [], page: 1, pageSize: 20, totalCount: 0 }` | Mount the island | Empty-store message rendered (distinct from the loading indicator and from error styling); **no** error announced; total shows `0`. Loading state must not still be visible | REQ-UI-001; VER-UI-002; C7 |
| IT-UI-003 | Fetch double returns 200 `{ items: [], page: 1, pageSize: 20, totalCount: 0 }` for `status=Contacted` | Mount; change filter to Contacted (page resets to 1) | "No inquiries match this filter" message rendered (semantically distinct from the empty-store message of IT-UI-002 or the shared planned copy must distinguish store vs. filter); no rows; not an error state | REQ-UI-001; VER-UI-002; C4, C7 |
| IT-UI-004 | Fetch double rejects list call with `TypeError` (network failure) | Mount; then activate the visible "Retry" control | (1) Generic recoverable error message rendered and announced in the assertive live region (matches /could not load\|went wrong/i); raw exception text absent; no rows fabricated. (2) Retry re-issues `GET /api/inquiries`; on canned 200 envelope the error clears and rows render | REQ-UI-001; VER-UI-002; C7 |

### 6.B Filter, sort, paging

| Case ID | Arrange / input | Act | Observable result | Traceability |
| --- | --- | --- | --- | --- |
| IT-UI-005 | Canned envelopes for `status=New` and `status=Closed`; island mounted, page 2 active (paged there first) | Change filter select to `Closed` | Exactly one new list request: `/api/inquiries?status=Closed&page=1` — page reset to 1 (C7), canonical casing; rendered rows come from the `Closed` envelope; the `New` response (if stale/in flight) never renders (see IT-UI-010). Filter select exposes exactly the five canonical statuses plus "All statuses" | REQ-UI-001; VER-UI-001; C2, C7 |
| IT-UI-006 | Island mounted with `status=Contacted` active | Change filter select back to "All statuses" | New list request is `/api/inquiries` **without** a `status` parameter (omitted means all statuses including `Closed`; `status=` is invalid per C2); rows re-render from the unfiltered envelope | REQ-UI-001; VER-UI-002; C2, C4, C7 |
| IT-UI-007 | Seed ids 1–45, `pageSize` 20, filter `New`, sort default; canned envelopes per page | Click "Next" then "Previous" | Requests are `/api/inquiries?status=New&page=2` then `…page=1`; page indicator follows the response envelope (`page`, `totalCount`), never accumulated client guesses; "Next" disabled/offered per envelope (`page 3 of 3` style) — totals always reconcile to the latest envelope | REQ-UI-001; REQ-API-004; VER-UI-001; C4 |
| IT-UI-008 | Canned envelopes for `sort=createdDateAsc` and `sort=createdDateDesc` | Toggle sort control to ascending, then descending | Requests carry exactly `sort=createdDateAsc` then `sort=createdDateDesc` (C4 exact spelling); rendered row order matches each response's `items` array order (assert O'Neill vs Fernández swap), independent of local re-sorting | REQ-UI-001; REQ-API-004; VER-UI-002; C4 |
| IT-UI-009 | Page 1 active showing `totalCount: 45`; next canned response for `page=2` is 200 `{ items: [], page: 2, pageSize: 20, totalCount: 20 }` (rows deleted concurrently — C4 non-snapshot) | Click "Next" | Island derives last page `ceil(20/20)=1`, **navigates to page 1 and refetches** `/api/inquiries?page=1`; page-1 rows render; page indicator shows 1; no permanent "empty page" state and no error | REQ-UI-001; VER-UI-002; C4, C7 |

### 6.C Stale responses and unmount

| Case ID | Arrange / input | Act | Observable result | Traceability |
| --- | --- | --- | --- | --- |
| IT-UI-010 | Mount with deferred list response D1 (`page=1` items 1–2); then click "Next" → deferred D2 (`page=2` items 21–22, `totalCount: 45`) | Resolve D2, then resolve D1 (older, out of order) | After D2: rows 21–22 render. After stale D1 resolves: **still** rows 21–22 — the older response never overwrites newer state (ordering protection; cancellation alone is insufficient, C7). Total remains `45` from the newer envelope | REQ-UI-001; VER-UI-002; C7 |
| IT-UI-011 | Mount with list call pending (deferred D1) | Unmount the island; then resolve D1; also unmount with a pending detail call and resolve it | Resolution after unmount triggers **no** exception, no React "state update on unmounted component" error (console.error spy stays clean), no live-region/DOM writes, and no unhandled rejection | REQ-UI-001; VER-UI-002; C7 |

### 6.D Detail panel

| Case ID | Arrange / input | Act | Observable result | Traceability |
| --- | --- | --- | --- | --- |
| IT-UI-012 | Canned 200 `GET /api/inquiries/1` = FIX-INQ-1; list rendered | Activate the row opener button for O'Neill | `GET /api/inquiries/1` issued; panel renders **all** eleven fields: id `1`, both names, email, phone `+1 555 0100`, course, location `Calgary NW`, message, status `New`, created `2026-09-10`, updated `2026-09-10`. With FIX-INQ-2 the three null fields render as explicit empty markers ("—"), never the string `"null"`; focus moves into the panel (full cycle in IT-UI-028) | REQ-UI-001; VER-UI-001; C1, C7 |
| IT-UI-013 | Detail responses deferred: D1 for id 1 opened, then (without closing) D2 for id 2 opened | Resolve D2 (FIX-INQ-2), then resolve D1 (FIX-INQ-1) | Panel shows Fernández/FIX-INQ-2 fields after D2; **after stale D1 resolves the panel still shows FIX-INQ-2** — a newer selection's result is never overwritten by an older detail response (C7) | REQ-UI-001; VER-UI-002; C7 |
| IT-UI-014 | Fetch double answers `GET /api/inquiries/1` with the 404 `ProblemDetails` fixture (§4) | Open detail for row 1 | Record-gone message announced (matches /no longer exists\|not found\|gone/i); stale detail panel **cleared/closed** (no row-1 fields remain); island then issues a list refresh (`GET /api/inquiries` with current filter/page) and re-renders from it | REQ-UI-001; REQ-API-003; VER-UI-002; C3, C7 |

### 6.E Status updates

| Case ID | Arrange / input | Act | Observable result | Traceability |
| --- | --- | --- | --- | --- |
| IT-UI-015 | Row 1 (status `New`) rendered; canned 200 PUT response = FIX-INQ-1 with status `Contacted`, `updatedDate 2026-09-14T10:15:00Z`; subsequent list refresh canned | Set row select to `Contacted`; click "Apply" | PUT issued to `/api/inquiries/1/status`, `Content-Type: application/json`, body exactly `{"status":"Contacted"}`; after response, island refreshes the list (`GET /api/inquiries` with current filter/page); success message in polite live region (matches /saved/i); row then shows `Contacted` | REQ-UI-001; VER-UI-001; C2, C7 |
| IT-UI-016 | Filter `New` active; rows 1–2 (`New`); update row 1 to `Contacted`; refresh envelope for `status=New` now contains only row 2, `totalCount: 1` | Apply the update (IT-UI-015 flow) | Post-update refresh is `GET /api/inquiries?status=New&page=1`; row 1 **disappears** (it left the active filter); total reconciles to `1`; success announcement still shown — disappearance is not reported as failure | REQ-UI-001; VER-UI-002; C4, C7 |
| IT-UI-017 | Filter `Contacted`; page 2 was showing only the last matching row (previous envelope `totalCount: 21`); update that row to `Closed`; canned refresh response for `page=2`: `{ items: [], page: 2, pageSize: 20, totalCount: 20 }`; canned `page=1` response: 20 `Closed` items | Apply the update | Post-update refresh returns the empty page with the reduced total; island computes last page `ceil(20/20)=1`, navigates to page 1, and refetches `GET /api/inquiries?status=Closed&page=1`; 20 rows render; page indicator shows 1; never an empty dead end, never page 0 | VER-UI-002; C4, C7 |
| IT-UI-018 | Row 1 currently `New`; canned PUT same-status response returns FIX-INQ-1 **unchanged** (`updatedDate` still `2026-09-10T14:00:00Z`, C2 no-op) | Apply `New` on row 1 | 200 handled as success: polite live region announces saved; rendered `updatedDate` for the row stays `2026-09-10`; refresh issued and state matches the unchanged response — no fabricated change, no error | REQ-UI-001; VER-APP-002; C2 |
| IT-UI-019 | PUT pending (deferred) | Click "Apply" for row 1; while pending, click "Apply" again (and re-change the select) | During the in-flight PUT the Apply control is disabled/`aria-disabled` and re-clicks issue **no** additional request — exactly **one** PUT total; after resolve, control re-enabled | VER-UI-002; C7 |
| IT-UI-020 | Canned PUT response: 400 `ValidationProblemDetails` (errors key `status`) | Apply a status change on row 1 | No success announcement; row still renders its persisted status `New`; assertive live region shows the validation message derived from the safe ProblemDetails (field identifier surfaced; the attempted value, stack, or raw JSON never rendered); refresh **not** required but if issued must not replace row state with the failed value | REQ-UI-001; REQ-SYS-005; VER-UI-002; C3, C7 |
| IT-UI-021 | Canned PUT response: 404 `ProblemDetails` | Apply a status change on row 1 | Record-gone message announced; stale detail (if open) cleared; island refreshes the list and the missing row is gone from the render; no success message | REQ-API-003; VER-UI-002; C3, C7 |
| IT-UI-022 | PUT rejects with `TypeError` (network failure) | Apply a status change on row 1 | Generic recoverable failure message (matches /could not be saved\|try again/i) in the assertive live region; **no** success announcement; row keeps persisted status `New`; Apply re-enabled and a retry is possible (second attempt succeeds with canned 200 and normal IT-UI-015 flow follows) | VER-UI-002; C7 |
| IT-UI-023 | Canned PUT 200 (FIX-INQ-1 → `Contacted`, `updatedDate 2026-09-14T10:15:00Z`); the follow-up list refresh rejects (`TypeError`) | Apply a status change on row 1 | Message contains **both** parts: saved confirmation (/saved/i) **and** refresh-failure warning (/refresh failed\|out of date/i) — never the plain "save failed" wording; row 1 renders the new status `Contacted` taken from the PUT response; totals remain from the last successful envelope | VER-UI-002; C7 |

### 6.F Safe and robust rendering

| Case ID | Arrange / input | Act | Observable result | Traceability |
| --- | --- | --- | --- | --- |
| IT-UI-024 | List and detail served with FIX-INQ-XSS (and FIX-INQ-2 for Unicode `Céline Fernández`) | Mount; open the XSS row's detail | `window.__xss` stays `undefined`; no `<img>`/`<script>` element exists inside the table or panel (`container.querySelector('img, script')` is null); the literal markup appears only as **text** (`<img src=x…` visible in the cell's `textContent`); Unicode, apostrophes, `&`, and quotes render verbatim — never truncated, lowercased, or HTML-mangled (C1) | REQ-UI-001; VER-UI-002; C1, C7 |
| IT-UI-025 | Error variants: (a) 502 with `Content-Type: text/html` body `<html><body>boom</body></html>`; (b) 500 with `Content-Type: application/json` body `{"foo":1}` (wrong shape) | Trigger list load; separately trigger an update, for each variant | Generic recoverable message rendered and announced for both; **no** parsing crash (component stays mounted and operable); the raw HTML body is never injected into the DOM (`queryByText(/boom/)` null); no success announcement on failed updates | REQ-SYS-005; VER-UI-002; C3, C7 |
| IT-UI-026 | Canned 500 `ProblemDetails` whose (non-canonical) body additionally carries `"detail"` and `"exception"` fields containing the sentinel strings `SELECT * FROM CourseInquiries` and `Server=tcp:test` | Trigger a list load | Only the safe `title` (or generic fallback) renders; the sentinel strings are **absent** from the DOM; assertive live region announces the failure; page remains interactive | REQ-SYS-005; VER-UI-002; C3 |

### 6.G Accessibility, focus, live regions

| Case ID | Arrange / input | Act | Observable result | Traceability |
| --- | --- | --- | --- | --- |
| IT-UI-027 | Standard two-row list mounted | Query by accessibility: `getByLabelText('Filter by status')`; inspect row selects/Apply labels (e.g. "Status for Avery O'Neill", "Apply for Avery O'Neill"); press `Tab` through filter → first row opener → row select → Apply; press `Enter` on the focused opener | All controls have programmatic labels (no orphan inputs); table exposes `columnheader` roles (semantic `th`); keyboard traversal reaches every action in DOM order; `Enter` (and `Space`) on the opener opens the detail exactly as a click does (IT-UI-012) | REQ-UI-001; VER-UI-001; C7 |
| IT-UI-028 | Detail panel closed; list rendered | Open detail via opener click → record `document.activeElement`; press `Escape`; reopen; click the panel's close button | On open, focus moves **into** the panel (activeElement inside the panel container); `Escape` closes the panel and returns focus **to the originating opener button**; close button behaves identically; focus never lands on `<body>` | VER-UI-001; C7 |
| IT-UI-029 | Two-row list; canned success and canned failure responses | Perform a successful update (IT-UI-015), then a failed update (IT-UI-022) | Success text appears inside the `role="status"` region; failure text inside the `role="alert"` region (not color-only: both messages are real text; status column renders the literal status word `New`/`Contacted`, never a colored dot alone) | VER-UI-001; C7 |

### 6.H Scope guards

| Case ID | Arrange / input | Act | Observable result | Traceability |
| --- | --- | --- | --- | --- |
| IT-UI-030 | Fully populated list and open detail panel | Query the whole container for create/delete affordances | No button/control matches /create\|add inquiry\|delete\|remove/i anywhere in the island (list, detail, filter bar). Deletion remains a hard-delete API operation (ADR-0006) and creation an API/Swagger operation — the island never grows them silently (C7, ADR-0009) | REQ-UI-001; VER-UI-001; C7 |

## 7. Host integration cases (IT-HOST)

Real Razor shell + **actual compiled production assets**, xUnit `Category=Integration`
(§2.2). Fixture: build once (`pnpm --dir frontend build`), run the host
in-process, and **assert the dev server is absent** (a TCP connect to the Vite
dev port fails) before every case. Tests read the emitted manifest from disk and
fetch every referenced asset over HTTP.

| Case ID | Arrange / input | Act | Observable result | Traceability |
| --- | --- | --- | --- | --- |
| IT-HOST-001 | Production build emitted; manifest parsed (missing manifest = Blocked with reason "frontend build not produced") | `GET /dashboard` | 200 `text/html`; exactly one island mount element (planned `<div id="dashboard-root">`); exactly one `<script type="module" src="…">` whose `src` equals the manifest's entry `file` for `index.html` served under `/app/`; one `<link rel="stylesheet">` per manifest `css[]` entry; body contains **none** of: `:5173`, `@vite/client`, `/src/main.tsx`, `node_modules` | REQ-UI-001; VER-UI-001; C7; ADR-0004 |
| IT-HOST-002 | Manifest parsed; transitive closure computed from the entry (`file`, `css[]`, `imports`, `dynamicImports`) | `GET` every referenced asset path under the public base | Every referenced file — entry JS, **CSS**, and every imported/preloaded chunk — returns 200 with nonempty body; JS content-type is `text/javascript` or `application/javascript`; CSS is `text/css`; closure count ≥ 2 (entry + at least one CSS or chunk) proving real emission, not a stub | REQ-UI-001; VER-UI-001; C7 |
| IT-HOST-003 | Host running; seed one inquiry via the real API (`POST /api/inquiries` with FIX-INQ-1 fields) | `GET /dashboard` HTML | Contains a `<noscript>` block whose text mentions JavaScript (no-JS guidance, C7); contains a loading placeholder for the island (planned text matches /loading/i); contains **no** server-rendered inquiry data — the seeded email `avery.oneill@example.com` is absent from the HTML (shell supplies layout and guidance, not a functional queue; ADR-0009) | REQ-UI-001; VER-UI-001; C7 |
| IT-HOST-004 | Create sentinel file `backend/wwwroot/favicon.ico`; delete the emitted `app/` subfolder; re-run `pnpm --dir frontend build` | Inspect `wwwroot` tree; `GET /favicon.ico` and `GET /app/<entry>` | Rebuild recreates all assets **under the dedicated subfolder** `app/` only; the pre-existing `favicon.ico` survives (200) — cleaning a build never deletes unrelated static files (C7); rebuilt manifest present and entry fetches 200 | REQ-UI-001; VER-UI-001; C7 |

## 8. SQL Server lane setup and safety (IT-SQL)

Lane rules (all cases; §2.3 policy):

1. **Connection.** `SQLSERVER_TEST_CONNECTION_STRING` must target a **disposable**
   server (CI service container or throwaway local instance). The harness
   **refuses to run** against a database whose name is not prefixed
   `CourseInquiryTests_` — a guard against pointing the lane at any shared or
   production database. No secrets are committed.
2. **Isolation.** Each run creates a uniquely named empty database
   (`CourseInquiryTests_<8hex>`), executes the script there, and drops it in a
   `finally`. Nothing outside that database is read or written. The application
   runtime (SQLite) never touches this lane; this lane never touches the app.
3. **Execution.** The script is executed **verbatim** (GO-separated batches via
   `sqlcmd`, or equivalent batch splitting). The three report queries are
   executed as **the exact text extracted from `database/database.sql`** (planned
   script contract: each query delimited by `-- query: last-7-days`,
   `-- query: count-by-status`, `-- query: duplicate-email` marker comments, with
   `@AsOf` read as a variable the harness may declare before execution). The
   harness never re-implements query logic and never substitutes SQLite. A
   missing marker or missing `database.sql` **fails** the case.
4. **Fixed `@AsOf`.** Time-dependent verification declares
   `DECLARE @AsOf datetime2(7) = '2026-09-10T14:00:00Z';` and runs the script's
   own query against it (C8 explicitly allows supplying a fixed `@AsOf` to the
   same query instead of copying its logic).
5. **Blocked evidence.** Unreachable server or unset variable ⇒ every case
   reports **Blocked** (never green). Release pipeline requires the lane to pass;
   local default runs exclude it via the `Category!=SqlServer` filter.

## 9. SQL integration cases (IT-SQL)

| Case ID | Arrange / input | Act | Observable result | Traceability |
| --- | --- | --- | --- | --- |
| IT-SQL-001 | Empty disposable DB `CourseInquiryTests_<8hex>`; committed `database/database.sql` | Execute the script verbatim (GO batches); then `SELECT COUNT(*) FROM CourseInquiries` | Script completes with no errors; table `dbo.CourseInquiries` exists; `COUNT(*) >= 5`; the samples collectively cover **all five** canonical statuses (C2) and include at least one row with each optional column `NULL` and one with values | REQ-DATA-002; VER-DATA-002; C8 |
| IT-SQL-002 | IT-SQL-001 database | Read column metadata from `INFORMATION_SCHEMA.COLUMNS` (+ identity/PK/constraint catalogs); insert an explicit-`Id` row without `IDENTITY_INSERT` | Columns match the C1 mapping exactly: `Id` `int` NOT NULL identity(1,1) PK; `FirstName` `nvarchar(100)` NOT NULL; `LastName` `nvarchar(100)` NOT NULL; `Email` `nvarchar(254)` NOT NULL; `Phone` `nvarchar(50)` NULL; `CourseName` `nvarchar(200)` NOT NULL; `PreferredLocation` `nvarchar(200)` NULL; `Message` `nvarchar(4000)` NULL; `Status` `nvarchar(≥10)` NOT NULL; `CreatedDate`/`UpdatedDate` `datetime2(7)` NOT NULL. Identity insert attempt **fails** (identity, not nullable-suppliable); **no unique index/constraint exists on `Email`** — a second row with an identical email inserts successfully. No `rowversion`/timestamp column | REQ-DATA-002; VER-DATA-002; C1, C2, C8 |
| IT-SQL-003 | IT-SQL-001 database | (a) `INSERT` rows with each of the five canonical status names; (b) `INSERT` a row with `Status = 'Unknown'` | (a) all five insert successfully; (b) the insert **fails** (status domain CHECK constraint or equivalent) — stored statuses are constrained to the five C2 values, not free text | REQ-DATA-002; VER-DATA-002; C2, C8 |
| IT-SQL-004 | IT-SQL-001 database; snapshot `COUNT(*)` and per-row values before | Execute the script a **second** time against the now-populated table | Either the re-run **fails cleanly** (expected "object already exists" class of error) or it succeeds idempotently — in both outcomes `COUNT(*)` is unchanged, no sample row's values were altered, and no table was dropped/truncated silently (C8: re-running need not succeed; must never silently drop real data) | REQ-DATA-002; C8 |
| IT-SQL-005 | DB seeded via script samples **plus** explicit rows (Email, CreatedDate UTC): `old@example.com` `2026-09-02T13:59:59Z`; `boundary@example.com` `2026-09-03T14:00:00Z`; `now@example.com` `2026-09-10T14:00:00Z`; `future@example.com` `2026-09-10T14:00:01Z`. `@AsOf` = `2026-09-10T14:00:00Z` | Execute the script's **exact** last-7-days query text with the fixed `@AsOf` | Result contains exactly the rows for `boundary@example.com` and `now@example.com`: inclusive at `CreatedDate >= DATEADD(day,-7,@AsOf)` and at `CreatedDate <= @AsOf`; the older row and the **future** row are excluded. Rolling interval semantics, not calendar dates (C8) | REQ-DATA-002; VER-DATA-002; C8 |
| IT-SQL-006 | DB seeded with statuses: New×2, Contacted×1, Pending×1, Registered×1, Closed×2 | (a) Run the script's count-by-status query; (b) hard-`DELETE` one `New` row and all `Registered` rows; re-run the query | (a) groups exactly: New=2, Contacted=1, Pending=1, Registered=1, **Closed=2** (Closed counted like any status); (b) New=1 and **no `Registered` group at all** — only represented statuses produce groups; hard-deleted rows do not count (C8, ADR-0006) | REQ-DATA-002; VER-DATA-002; C8 |
| IT-SQL-007 | DB seeded with emails: `Avery@example.com`; `  avery@example.com  ` (surrounding spaces); `AVERY@EXAMPLE.COM`; `bia@example.com` + `Bia@example.com`; `unique@example.com` | (a) Run the script's duplicate-email query; (b) `SELECT Email` raw for the affected rows | (a) Groups by `LOWER(LTRIM(RTRIM(Email)))` with `COUNT(*) > 1`: exactly two groups — `avery@example.com` = **3**, `bia@example.com` = **2**; `unique@example.com` absent; (b) stored values **retain** original casing/whitespace — the reporting normalization never modifies data, and the duplicate insert succeeded because email is not unique (C8, C1) | REQ-DATA-002; VER-DATA-002; C8 |
| IT-SQL-008 | DB seeded via script; one explicit sample inserted with `CreatedDate = '2026-09-10T14:00:00.1234567Z'` | Query `INFORMATION_SCHEMA.COLUMNS` for the date columns' type/precision; `SELECT` the explicit sample's `CreatedDate` | `DATA_TYPE = datetime2`, `DATETIME_PRECISION = 7`; the stored value reads back as exactly `2026-09-10 14:00:00.1234567` — the UTC instant is preserved with full precision and **no local-offset shift** (C8) | REQ-DATA-002; C8 |

## 10. Supplemental real-browser walkthroughs (MAN-UI)

Run against the **real app** (production frontend build + `dotnet run`, SQLite
runtime store) in a real browser with **synthetic data only**. Evidence:
screenshots/short notes appended to the task or PR description. These
walkthroughs prove actual runtime mounting, layout, keyboard, and no-JS
behavior; **they are not substitutes for any automated case above** and do not
require a permanent Playwright suite (ADR-0009 retains a walkthrough, not an
E2E suite).

| Case ID | Arrange / input | Act | Observable result | Traceability |
| --- | --- | --- | --- | --- |
| MAN-UI-001 | App running; several synthetic inquiries seeded via Swagger (mixed statuses, one with FIX-XSS text) | In the browser: open `/dashboard`, wait for mount, filter by `Contacted`, open a detail, change a row's status, hard-reload the page | Island mounts (no permanent loading state); list/filter/detail/status flows work end-to-end; after reload the status change is **persisted** (visible in list/detail); XSS row shows markup as inert text; browser console has **no** errors; success/error messages visible during the flow. Evidence: screenshots of each step | VER-UI-001; VER-SYS-001; REQ-UI-001; C7 |
| MAN-UI-002 | App running with data; keyboard only (no mouse) | Complete a full triage pass with `Tab`/`Enter`/`Escape`/arrow keys: filter → open detail → close → update a status | Every control reachable and operable by keyboard in DOM order; visible focus indicator at all times; detail close returns focus to its opener; success/error announcements observable via OS screen reader or the browser accessibility inspector (live regions) | VER-UI-001; REQ-UI-001; C7 |
| MAN-UI-003 | App running; disable JavaScript in the browser (setting or extension) | Open `/dashboard` | Server-rendered shell renders: layout, headings, and explicit no-JavaScript guidance; no broken/fake inquiry queue pretending to work; no console-crash; page explains that JavaScript is required for the queue (C7, ADR-0004/0009) | VER-UI-001; REQ-UI-001; C7 |
| MAN-UI-004 | App running with ≥ 5 inquiries | Inspect layout at desktop 1280px, mobile 375px, and 200% browser zoom; exercise filter and detail at each size | Table and detail remain readable and operable; no clipped or overlapping controls; status remains distinguishable without color (text label visible); screenshots at all three sizes attached | VER-UI-001; REQ-UI-001; C7 |

## 11. Traceability and coverage summary

- **ID ranges owned by this file:** UT-UI-001–005 (5), IT-UI-001–030 (30),
  IT-HOST-001–004 (4), IT-SQL-001–008 (8), MAN-UI-001–004 (4) — **51 cases**,
  all planned.
- **Identifiers cited:** REQ-UI-002/VER-UI-002 (UI async boundaries and safe
  accessible rendering) and REQ-DATA-002/VER-DATA-002 (SQL script & queries)
  exist in `model.json`; their verification procedures already point at this
  catalog's IT-UI/IT-HOST/MAN-UI and IT-SQL cases. REQ/VER-DATA-003,
  REQ/VER-APP-003, and REQ/VER-CRM-004 belong to the backend catalog
  ([backend-cases.md](backend-cases.md)) and are intentionally unused here.
- **Lane mapping (per [tdd-plan.md](tdd-plan.md)):** IT-HOST → xUnit
  `Category=Integration` with `CaseId` traits; IT-SQL → xUnit
  `Category=SqlServer` with `CaseId` traits (release lane only,
  `SQLSERVER_TEST_CONNECTION_STRING`); UT-UI/IT-UI → Vitest under
  `frontend/src/**/*.test.ts(x)` via the `test` script; MAN-UI → manual
  evidence, no suite.
- **Unresolved gaps:** none blocking. Two implementation contracts are pinned
  here for executability and must be honored or consciously revised with the
  catalogs in sync: the build-asset layout constant in §2.2 (subfolder/manifest
  path) and the script query markers `-- query: …` plus harness-suppliable
  `@AsOf` in §8. If either lands differently, update this catalog in the same
  change — do not weaken the cases.
- **Verification status:** every case in this file has executable passing
  evidence; all mapped model verifications are `passing`.
