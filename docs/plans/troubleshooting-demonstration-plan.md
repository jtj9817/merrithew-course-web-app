# Troubleshooting Demonstration — Live, Reproducible Proof of the Written Answer

- **Status:** implemented (September 17, 2026) — see [CHANGELOG](../CHANGELOG.md) and
  the [missing-inquiry runbook §7](../runbooks/missing-inquiry.md) walkthrough
- **Scope:** make the assessment's written **Troubleshooting** answer reproducible against
  the running app, so an assessor can watch each claim demonstrated in the live dashboard
- **Delivery decisions:** demo owns OBS-101 **GAP-6** and builds on the rest of OBS-101;
  driver is an **in-app DEV walkthrough + runbook** (no new toolchain); reconciliation is a
  **live dev endpoint + a static SQLite `.sql` file**
- **Related:** [written-answers.md](../../written-answers.md) (Troubleshooting),
  [OBS-101](../tickets/OBS-101-submission-observability-and-prevention.md),
  [contracts C4–C6](../architecture/contracts.md),
  [CRM simulation plan](crm-simulation-plan.md) (the pattern this mirrors)

## 1. Context

`written-answers.md` answers the assessment's **Troubleshooting** prompt — *"staff report
that some inquiries submitted through the website don't appear in the admin list"* — as a
reasoning-first document. The goal of this work is to make that reasoning **real and
reproducible against the running application**, so an assessor can watch each claim be
demonstrated in the actual dashboard rather than take the prose on faith.

The app already ships two dev subsystems that prove this is the right shape: **scenario
seeding** (`/api/dev/scenarios`, driven by `ScenarioSwitcher`) and **CRM outcome
simulation** (`/api/dev/crm-simulation`, driven by `CrmSimulationControl`). The
demonstration extends that same opt-in, same-origin, dev-gated pattern to cover every branch
of the written answer.

**This plan builds on OBS-101 and owns GAP-6.** OBS-101 is being implemented in the working
tree now (uncommitted) and provides the observability the diagnosis reads: validation-rejection
logging (EventId 23), a correlation-ID scope + `traceId` in 400/500 ProblemDetails, the
creation-audit log (`InquiryService.cs:53`, EventId 3), the request-outcome middleware
(EventIds 20–22), `/health`, the `intake_requests` counters
(`created`/`validationRejected`/`serverError`), and a `FilteredStateIndicator` (GAP-7). This
plan **assumes those land** and delivers the pieces they don't: the ability to *stage* each
failure on demand, a staged dataset, and the currently-unbuilt **GAP-6** (SQLite
reconciliation queries + the runbook).

## 2. What each branch needs, and who provides it

| Written-answer branch | Provided by |
| --- | --- |
| Stored-but-hidden by a status filter (the "usual bug") | **New** `missing-inquiries` scenario + OBS-101 `FilteredStateIndicator` |
| On another page (paged snapshot, C4) | Existing `pagination` scenario / same dataset |
| Never stored — validation 400 | Real `POST /api/inquiries` (bad payload) + OBS-101 GAP-1 log/metric |
| **Genuine 5xx / backend-DB failure — never stored** | **New** intake fault-injection tool → real sanitized 500 |
| CRM failure never causes a missing inquiry (C5) | Existing `CrmSimulationControl` |
| Backend logs (ErrorType, creation, CRM, correlation ID) | OBS-101 (already in tree) |
| Reconciliation SQL on SQLite (count-by-status / last-7-days / duplicate-email / by-email) | **New (GAP-6)** dev endpoint + static `.sql` |
| Duplicate from resubmit (non-idempotent intake, C5) | **New** dataset seeds a duplicate email |

## 3. Build

### 3.1 `missing-inquiries` staging scenario

- **`backend/DevTools/ScenarioCatalog.cs`** — add a `missing-inquiries` entry to `All`, with a
  private `BuildMissingInquiries()` builder (mirror the existing `Demo`/`GenerateLoad` style).
  Compose ~24–28 rows tuned for SQLite + the default page size
  (`InquiryListQuery.DefaultPageSize = 20`, sort `createdDateDesc`):
  - A visible backlog of `Registered`/`Contacted`/`Closed`/`Pending` rows, **plus several
    recent `New` rows** — so a staffer with a non-`All` filter active sees a full-looking list
    while the `New` "missing" inquiries are hidden.
  - **Two rows sharing one email** (a visitor who resubmitted after a timeout) so the
    duplicate-email reconciliation returns a real group (C5 / non-idempotent intake).
  - Enough total rows (> 20) that an older inquiry lands on page 2 to illustrate paging.
- **`backend/DevScenarios.http`** — add a `POST .../scenarios/missing-inquiries` example.
- No seeder change: `ScenarioSeeder` already creates through the real `CreateAsync` path and
  stamps `CreatedDate` at seed time (all within the last-7-days window — convenient for that
  query). Ordering constraint: the seeder runs `CreateAsync`, so **the intake fault must be
  disarmed while seeding** (it is, by default; see §3.2).

### 3.2 Intake fault-injection dev tool (the genuine-5xx gap) — mirror the CRM simulator

- **`backend/DevTools/IntakeFaultRuntime.cs`** (new) — thread-safe singleton holding an "arm
  the next N creates to fail" counter (default `0` = inert) and the last outcome, with
  `Arm(int)`, `Disarm()`, and `bool TryConsume()`. Model it on `CrmSimulationRuntime`
  (`backend/Services/CrmSimulation.cs`): `Volatile`/interlocked access, bounded state.
- **`backend/Program.cs`** — register `AddSingleton<IntakeFaultRuntime>()` **always** (exactly
  as `CrmSimulationRuntime` is registered at `Program.cs:63` regardless of gating); map a new
  `IntakeFaultEndpoints` group only when enabled. Because the endpoint is the only way to arm
  it, an unmapped (production) runtime is permanently inert. **No error-middleware change
  needed** — a thrown fault flows through the existing handler, which already logs
  `ErrorType`, increments `intake_requests{outcome=serverError}`, and returns a 500
  ProblemDetails carrying `traceId` (`Program.cs:121–140`).
- **`backend/Services/InquiryService.cs`** — add `IntakeFaultRuntime intakeFault` to the
  primary constructor; at the top of `CreateAsync` (after the existing
  `cancellationToken.ThrowIfCancellationRequested()`, **before** `db.CourseInquiries.Add`):
  `if (intakeFault.TryConsume()) throw new IntakeFaultInjectedException();`. Throwing before the
  `Add`/`SaveChangesAsync` guarantees **nothing is persisted** — a faithful "backend/DB failure
  → never stored" case. Add a small `IntakeFaultInjectedException` type (its name is what
  surfaces as the sanitized `ErrorType`, which is fine — non-PII).
- **`backend/DevTools/IntakeFaultEndpoints.cs`** (new) — mirror `CrmSimulationEndpoints`:
  `GET /api/dev/intake-fault` (armed count + last result), `PUT /api/dev/intake-fault`
  (validated `armCount`, range-checked → `ValidationProblem` on bad input, never a 500).
- **`backend/DevTools/DevToolsOptions.cs`** — add `IntakeFaultKey = "DevTools:IntakeFault"` and
  `IntakeFaultEnabled(env, config)` (Development or explicit flag), matching the two existing
  gates.
- **Frontend** — `frontend/src/lib/intakeFault.ts` (typed client + `parse*` guards +
  `intakeFaultToolsEnabled()` reading `window.__intakeFaultTools`) and
  `frontend/src/components/IntakeFaultControl.tsx`, both modeled on the CRM-sim pair. A primary
  **"Arm one failure and submit"** action: PUT `armCount=1` → real `POST /api/inquiries` →
  expect **500** → report the `traceId` and that **no row was stored**, then refresh so the
  queue visibly does *not* contain it.
- **Shell flag** — `backend/Pages/Dashboard.cshtml.cs` (add `IntakeFaultToolsEnabled`) +
  `backend/Pages/Dashboard.cshtml` (`window.__intakeFaultTools = true`) +
  `frontend/src/vite-env.d.ts` (declare the flag).

### 3.3 SQLite reconciliation — live endpoint + static `.sql` (GAP-6, part 1)

- **`backend/DevTools/ReconciliationEndpoints.cs`** (new) — read-only, dev-gated
  (`DevTools:Reconciliation` key + Development). All queries are **EF Core LINQ over
  `AppDbContext`** (parameterized; no raw SQL — consistent with the Security answer):
  - `GET /api/dev/reconciliation` → JSON
    `{ countByStatus (all five, incl. 0), last7DaysCount, duplicateEmailGroups: [{ normalizedEmail, occurrenceCount }] }`.
  - `GET /api/dev/reconciliation/by-email?email=…` → matching rows (the *stored-but-hidden vs
    never-stored* settle). Returns visitor rows → dev-only + synthetic-data caveat, documented.
- **`database/reconciliation.sqlite.sql`** (new) — SQLite-dialect equivalents of the three
  [`database/database.sql`](../../database/database.sql) reports + the direct lookup, runnable
  via `sqlite3 backend/inquiries.db < database/reconciliation.sqlite.sql`. Port notes:
  `lower(trim(Email))` for the duplicate normalization; `CreatedDate >= datetime('now','-7 days')`
  for the window (**verify against EF's stored text format** — EF SQLite stores `DateTime` as
  ISO-8601 text via the `UtcDateTimeConverter`; lexicographic comparison holds). Header comment
  states it targets the live runtime DB, unlike the SQL-Server `database.sql`.
- **Optional DEV readout** — a small `ReconciliationPanel.tsx` in the dev-tools area that
  fetches `/api/dev/reconciliation` and renders the counts, so the reconciliation is visible
  *in the website* during the walkthrough (recommended; keep it read-only and minimal).

### 3.4 The runbook + in-app walkthrough driver (GAP-6, part 2)

- **`docs/runbooks/missing-inquiry.md`** (new — the exact path OBS-101 GAP-6 prescribes). The
  guided, cheapest-first diagnosis. Each step lists: the **written-answers.md claim** it proves
  → the **live action** (which DEV control / endpoint / SQL) → the **expected evidence** (HTTP
  status, log EventId/line, metric tag, or count). Includes the **stored-but-hidden vs
  never-stored decision tree** and how to use the correlation `traceId` to tie a staff report
  to its request. Walkthrough spine:
  1. Seed `missing-inquiries`; set a non-`All` filter → recent `New` rows vanish; the
     `FilteredStateIndicator` ("Showing N of M · filtered by …") names the cause. Clear filter
     → they reappear. (Display layer, C4.)
  2. Reconciliation readout / `.sql`: count-by-status vs what staff see; last-7-days confirms
     recent submissions landed; duplicate-email surfaces the resubmit group. (C5.)
  3. CRM simulator → `PermanentFailure`/`Timeout`: submit succeeds (201), row present, CRM logs
     show the isolated failure → proves a CRM problem **cannot** cause a missing inquiry (C5)
     and rules out a whole layer.
  4. Bad `POST /api/inquiries` → 400 + `validationRejected` log/metric + `traceId` → "never
     stored because rejected," now confirmable from logs (OBS-101 GAP-1).
  5. Intake fault tool → real 500 + `serverError` metric + `ErrorType` + `traceId`; the row is
     absent → the genuine backend/DB-failure "never stored" case.
- **Driver = in-app DEV walkthrough**: the runbook narrates operating the DEV bar
  (`ScenarioSwitcher`, `CrmSimulationControl`, new `IntakeFaultControl`, reconciliation readout)
  in the live browser. No new toolchain. (Playwright automation is explicitly out of scope per
  the delivery decision.)

### 3.5 Tests & docs

- **Backend** (mirror `CrmRuntimeSimulationTests` + the CRM integration cases):
  - Unit: `IntakeFaultRuntime` arm/consume/disarm; reconciliation query correctness against a
    seeded context (counts, 7-day window, duplicate grouping, by-email).
  - Integration (reuse `InquiryApplicationFactory` / `LogCaptureProvider`): intake-fault
    endpoint shape + validation 400 + **armed `POST /api/inquiries` returns 500 and the row is
    absent from a fresh context**, with `serverError` metric + `ErrorType` log + `traceId`;
    reconciliation endpoint shape; and a production-shaped host (`DevTools:*` unset) does
    **not** map either new group (404). Extend the privacy sentinel scan to the new paths.
- **Frontend**: component tests for `IntakeFaultControl` (arm-and-submit → 500 wording, row
  absent, disabled/busy, no mount-time request when the flag is off) and the reconciliation
  readout, using the existing fetch-double pattern.
- **Docs**: `docs/backend/README.md` (new dev endpoints + gating), `docs/frontend/README.md`
  (new dev controls + flag), `docs/CHANGELOG.md` (Added). Tick **OBS-101 GAP-6** and add a
  reference from the ticket to the runbook (this work delivers it).

## 4. Verification (end-to-end, against the live app)

1. `dotnet test` and (in `frontend/`) `pnpm test` + `pnpm build` — all green, new suites pass.
2. `dotnet run --project backend`; open `/dashboard`. Walk the five runbook steps and confirm
   each expected-evidence item (filter indicator; 201 + present row under CRM failure; 400 +
   `validationRejected`; 500 + `serverError` + absent row). Watch the server console for the
   EventIds and the shared `correlationId`.
3. `sqlite3 backend/inquiries.db < database/reconciliation.sqlite.sql` — confirm counts match
   the dashboard and the `/api/dev/reconciliation` JSON.
4. Confirm gating: with `DevTools:*` unset under a non-Development environment, the new
   endpoints 404 and the DEV controls do not render.

## 5. Coordination, gating & non-goals

- **Merge risk with in-flight OBS-101.** This plan edits files OBS-101 is currently changing
  (`Program.cs`, `InquiryService.cs`, `App.tsx`, `Dashboard.cshtml(.cs)`, `vite-env.d.ts`, the
  test fixtures). Implement **after** the OBS-101 changes settle (or rebase onto them) to avoid
  clobbering; don't re-implement any observability OBS-101 already provides.
- **Dev-only & opt-in.** Every new surface is gated exactly like the existing dev tools
  (Development or an explicit `DevTools:*` flag); nothing is mapped or rendered in production.
  The app remains "synthetic data, local only" (per the Security answer) — relevant to the
  by-email lookup returning visitor rows.
- **Non-goals:** no Playwright/e2e toolchain (deferred); no visitor-facing form (Part 3 allows
  API/Swagger intake); no change to any Part-4 invariant (persist-first/sync-second C5, bounded
  private CRM work C6); no raw SQL in the runtime path.
