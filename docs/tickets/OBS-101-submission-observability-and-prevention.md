# [OBS-101] Submission Observability: Request-Outcome Logging, Correlation IDs, Creation Audit Trail & Prevention Measures

| Field | Value |
| :--- | :--- |
| **Issue Type** | Task / Observability Improvement |
| **Key** | OBS-101 |
| **Component** | `sub.api` (`backend/Controllers`, `backend/Program.cs`), `sub.app` (`backend/Services`), `sub.webui` (`frontend/src`) |
| **Priority** | High |
| **Status** | Open / Backlog |
| **Labels** | `observability`, `logging`, `correlation-id`, `diagnostics`, `troubleshooting`, `prevention`, `backend`, `assessment-gap` |

---

## 1. Summary & Background

The assessment's written **Troubleshooting** answer
([written-answers.md](../../written-answers.md)) describes how to investigate
"staff report that inquiries submitted through the website don't appear in the
admin list." A requirements audit against
[option-1-course-inquiry-dashboard.md](../../option-1-course-inquiry-dashboard.md)
Part 5 confirmed the answer covers all five required bullets — but it references
investigation artifacts and preventive measures that **do not exist in the
codebase as shipped**. The two most damaging omissions:

1. **A rejected submission leaves no trace.** Nothing logs validation
   rejections: the error middleware
   ([Program.cs:44](../../backend/Program.cs)) only logs *unhandled exceptions*
   (5xx), and `[ApiController]` auto-400s happen before any action code runs.
   For the exact reported scenario, the most common benign root cause —
   "never stored because validation rejected it" — is invisible in every log
   the system produces.
2. **A never-stored submission cannot be correlated to anything.** The only
   per-inquiry log entries (CRM outcome warnings,
   [InquiryService.cs:157](../../backend/Services/InquiryService.cs)) carry an
   `InquiryId` that exists only *after* the database commit, and there is no
   correlation/trace identifier propagated into request-scoped logs. A staff
   report ("visitor X submitted at 2pm") cannot be tied to any request.

This ticket closes those gaps plus the unimplemented prevention measures named
in the answer, so the documented investigation method is actually executable
against this system.

---

## 2. Current Implementation (Verified Baseline)

What already exists and was verified during the audit:

* **Sanitized 5xx logging.** The error middleware logs
  `Unhandled exception of type {ErrorType}` with no stack traces, SQL, or
  visitor data (C3/C6 compliant).
* **Per-inquiry CRM outcome logs.** `InquiryService` (EventIds 1–2) and
  `SimulatedCrmClient` (EventIds 10–14) emit allow-listed entries
  (`InquiryId`, attempt, outcome, `ErrorType`) — but only for inquiries that
  already committed.
* **Fatal startup on migration failure.** Unhandled `MigrateAsync()` at
  [Program.cs:63](../../backend/Program.cs) crashes startup (tested via
  `TestOnlyFatalMigration` / `ProcessRestartTests`).
* **Log-capture test fixture.**
  [LogCaptureProvider.cs](../../tests/CourseInquiryDashboard.Tests/Fixtures/LogCaptureProvider.cs)
  already renders structured log state in tests, including sentinels for the
  Phase 9 privacy scan.
* **Report SQL exists.** [database/database.sql](../../database/database.sql)
  ships the last-7-days, count-by-status, and duplicate-email queries the
  troubleshooting answer references (SQL Server dialect; the runtime DB is
  SQLite).

---

## 3. Identified Gaps

### [GAP-1] Validation rejections (400) produce no log entry

* **Affected**: `backend/Program.cs`, `backend/Controllers/InquiriesController.cs`
* **Description**: `[ApiController]` converts `ModelState` failures into
  `400 ValidationProblemDetails` via the automatic 400 pipeline before any
  controller action executes. No code observes or logs this outcome. The
  investigation path "a 400 means it was never stored" can be reasoned about
  but never *confirmed from logs* after the fact.
* **Remediation**: Configure
  `ApiBehaviorOptions.InvalidModelStateResponseFactory` (or a result filter) to
  emit one structured entry per rejected request — outcome category
  (e.g. `validationRejected`), the **field/property keys** that failed, status
  code, and the correlation ID from GAP-2. Log property *names* only; never
  attempted values (they contain visitor data — C6).

### [GAP-2] No correlation ID in request logs or error responses

* **Affected**: `backend/Program.cs`, `backend/Controllers`, `backend/Services`
* **Description**: No middleware pushes a correlation identifier into a logger
  scope, and the 500 `ProblemDetails` produced by the custom middleware (via
  `Results.Problem`) does not include `HttpContext.TraceIdentifier`, so even a
  staff member staring at an error response has nothing quotable to give an
  investigator. `AddProblemDetails()` is registered
  ([Program.cs:15](../../backend/Program.cs)) but the custom handler bypasses
  its automatic `traceId` extension.
* **Remediation**: Outermost middleware opens a logger scope carrying
  `HttpContext.TraceIdentifier` for the whole request (so request-outcome,
  validation-rejection, creation, CRM, and 5xx entries all share it) and adds
  `extensions["traceId"]` to every `ProblemDetails` the app emits. Accepting an
  inbound `X-Correlation-ID` header (gateway-provided) is a future option; not
  required here.

### [GAP-3] No creation-success audit entry

* **Affected**: `backend/Services/InquiryService.cs`
* **Description**: `CreateInquiryAsync` logs only CRM outcomes. There is no
  "inquiry stored" entry, so submissions-vs-stored-rows cannot be reconciled
  from logs, and the only evidence a specific inquiry committed is a
  side effect of the CRM sync logging.
* **Remediation**: After `SaveChangesAsync` succeeds (before CRM sync), emit an
  `Information`-level `LoggerMessage` — e.g. `Inquiry {InquiryId} created` —
  with a new unique `EventId` (3 is free in `InquiryService`; keep the
  source-generated `LoggerMessage` pattern). Allow-listed fields only.

### [GAP-4] No request-outcome logging for the intake/list endpoints

* **Affected**: `backend/Program.cs`
* **Description**: No request-logging middleware exists (verified: no
  `UseW3CLogging`, no request-logging middleware, bare Kestrel). The
  troubleshooting answer says to check "the POST status code and volume" —
  there is no in-app artifact recording either.
* **Remediation**: A terminal request-logging middleware (outermost, wrapping
  the error middleware) emits one entry per completed `/api/inquiries` request:
  method, route template, status code, outcome, correlation ID. Level by class:
  `Information` for 2xx, `Warning` for 4xx, `Error` for 5xx. Do not attach
  exceptions here — the existing error middleware remains the single place raw
  exception types are logged (C6).

### [GAP-5] No health endpoint or consumable failure counters

* **Affected**: `backend/Program.cs`
* **Description**: The prevention section calls for monitoring/alerting on POST
  error rates and logged CRM failures. Nothing exists: no health checks, no
  counters. (External alerting infrastructure is out of scope for this ticket —
  see §6 — but the system must expose something for it to consume.)
* **Remediation**:
  * `AddHealthChecks().AddDbContextCheck<AppDbContext>()` and
    `MapHealthChecks("/health")` (liveness + DB readiness in one endpoint for
    this scale).
  * A `System.Diagnostics.Metrics` meter with counters for intake outcomes
    (`created`, `validationRejected`, `serverError`) and CRM outcomes
    (`succeeded`, `retried`, `failed`, `timedOut`). Emits via the standard .NET
    meter; no external sink required.

### [GAP-6] No reconciliation runbook

* **Affected**: `docs/` (new), `database/`
* **Description**: The answer prescribes reconciling staff-visible counts
  against the database using the shipped report queries, but those are SQL
  Server dialect while the runtime database is SQLite — an operator following
  the answer today has no runnable commands.
* **Remediation**: Add `docs/runbooks/missing-inquiry.md` that walks the
  written-answer investigation end to end with **SQLite-dialect** equivalents of
  the three report queries, the log queries to run (by correlation ID /
  outcome / EventId), the `/health` check, and the "stored-but-hidden vs
  never-stored" decision tree.

### [GAP-7] Filtered state is only visible inside the select control

* **Affected**: `frontend/src/components/Toolbar.tsx`, `frontend/src/App.tsx`
* **Description**: The toolbar `<select>` shows the active filter, but there is
  no result-count/filtered indicator on the table itself, so a filtered view can
  still read as "inquiries are missing." (The *programmatic* announcement of
  filter result counts is already ticketed as
  [A11Y-101](A11Y-101-frontend-aoda-compliance.md) GAP-5 — do not duplicate it
  here.)
* **Remediation**: A visible indicator above the table — e.g. "Showing 4 of 126
  inquiries · filtered by Contacted" with a one-click "Clear filter" affordance
  when a non-`All` filter is active.

### [GAP-8] No automated coverage of the submit → list flow from the UI

* **Affected**: `tests/`
* **Description**: The answer admits the submit → list flow "is covered by
  IT-API/IT-APP integration tests, but not from a real form." There is no
  visitor-facing form (allowed by Part 3), so browser-level e2e is a stretch
  goal, not a requirement — the API-level coverage gap is already closed.
* **Remediation**: Optional/stretch only: a Playwright test driving the real
  dashboard bundle against a seeded backend (create via API, verify it appears
  in the list UI). Per the project spec, no headless-browser runner is required;
  do not block the ticket on this.

---

## 4. Acceptance Criteria

```gherkin
Scenario: Rejected submission leaves a traceable log entry
  Given the application is running
  When a POST /api/inquiries arrives with an invalid payload (missing field or bad email format)
  Then the API responds 400 with ValidationProblemDetails including a traceId extension
  And exactly one warning entry is logged with outcome "validationRejected"
  And the entry includes the correlation ID and the failing field keys
  And the entry contains no attempted field values, email, phone, name, or message text

Scenario: Successful submission logs a creation audit entry
  Given the CRM client is configured to fail every attempt
  When a valid POST /api/inquiries completes
  Then an information entry "Inquiry {InquiryId} created" is logged after the database commit
  And the CRM failure warnings for that inquiry carry the same correlation ID
  And the inquiry remains stored (REQ-SYS-003 regression guard still passes)

Scenario: Correlation ID ties a request's entries together
  Given request-outcome logging is installed
  When any /api/inquiries request completes with status 200, 201, 400, 404, or 500
  Then the request-outcome entry, any validation-rejection or creation entry,
       and any 5xx ErrorType entry all carry the same correlation ID
  And 500 ProblemDetails responses include extensions.traceId matching that correlation ID

Scenario: Health endpoint reports database readiness
  Given the application has started and migrations applied
  When GET /health is called
  Then the response is 200 with a healthy report
  And when the database is unavailable the response is 503

Scenario: Failure counters are emitted for consumption
  Given the metrics meter is registered
  When intake requests succeed, are rejected, or fail, and CRM syncs succeed or fail
  Then the corresponding counters increment and are visible via the .NET metrics instruments

Scenario: Filtered view is visually explicit
  Given the dashboard list is loaded and a status filter other than "All" is active
  Then the table shows a visible indicator of the filtered subset and total
  And a "Clear filter" control restores the unfiltered view
```

---

## 5. Technical Implementation Tasks

- [ ] **Correlation scope middleware (GAP-2)**
  - [ ] Outermost middleware in `Program.cs` pushes `HttpContext.TraceIdentifier` into a logger scope for the whole request (must wrap the existing error middleware).
  - [ ] Add `extensions["traceId"] = context.TraceIdentifier` to the 500 `ProblemDetails` in the error handler and to the `InvalidModelStateResponseFactory` response.
- [ ] **Validation-rejection logging (GAP-1)**
  - [ ] Configure `InvalidModelStateResponseFactory` to log one warning per rejected request: outcome `validationRejected`, failing field **keys** only, correlation ID.
- [ ] **Creation-success audit log (GAP-3)**
  - [ ] Add `LoggerMessage` (EventId 3, `Information`) in `InquiryService` after `SaveChangesAsync` commits, before CRM sync: `Inquiry {InquiryId} created`.
- [ ] **Request-outcome middleware (GAP-4)**
  - [ ] Terminal logging of method, route template, status, outcome, correlation ID for `/api/inquiries` requests; `Information`/`Warning`/`Error` by status class; never attach exceptions (leave 5xx detail to the existing handler).
- [ ] **Health checks & metrics (GAP-5)**
  - [ ] `AddHealthChecks().AddDbContextCheck<AppDbContext>()` + `MapHealthChecks("/health")`.
  - [ ] `System.Diagnostics.Metrics` counters for intake outcomes and CRM outcomes; wire increments next to the existing log call sites.
- [ ] **Reconciliation runbook (GAP-6)**
  - [ ] Create `docs/runbooks/missing-inquiry.md` with SQLite-dialect report queries, log queries by correlation ID/outcome, `/health` check, and the stored-but-hidden vs never-stored decision tree.
- [ ] **Filtered-state indicator (GAP-7)**
  - [ ] Visible "Showing N of M · filtered by X" indicator + "Clear filter" control in the dashboard; coordinate with A11Y-101 GAP-5 for the live-region announcement.
- [ ] **Tests**
  - [ ] Extend `InquiriesApiTests` (using `LogCaptureProvider` / `InquiryApplicationFactory`): 400 → `validationRejected` entry with field keys and correlation ID; 201 → creation entry; shared correlation ID across request-outcome, creation, and CRM entries; `traceId` present in 400/500 ProblemDetails.
  - [ ] Privacy guard: extend the sentinel scan so no new entry includes visitor field values (email/phone/name/message).
  - [ ] Health endpoint test (healthy + DB-unavailable 503 path).
  - [ ] Frontend unit test for the filtered-state indicator.
  - [ ] Run full suites (`dotnet test`, frontend tests).
- [ ] **Documentation updates**
  - [ ] Record the new log events/EventIds and correlation behavior in `docs/architecture/contracts.md` (C3 results/errors, C6 logging privacy).
  - [ ] Update the Troubleshooting prevention paragraph in `written-answers.md` to reflect implemented (vs aspirational) measures.
  - [ ] Optional/stretch (GAP-8): Playwright submit → list test; skip without blocking.

---

## 6. Out of Scope

* **Reverse proxy / W3C access logs** — deferred by decision; in-app
  request-outcome logging (GAP-4) covers the investigation need for now.
* **External monitoring/alerting infrastructure, on-call, dashboards** — this
  ticket only emits health state and counters for such a system to consume.
* **Authentication / authorization** — tracked separately in
  `docs/architecture/future/authentication-authorization-audit.md`; noted as
  the biggest security gap in the written Security answer.
* **Visitor-facing submission form** (and therefore in-form validation-error
  surfacing) — Part 3 explicitly permits API/Swagger intake with a
  list/filter UI; revisit if a public form is ever built.

---

## 7. References

* [written-answers.md](../../written-answers.md) — Troubleshooting section this ticket operationalizes
* [option-1-course-inquiry-dashboard.md](../../option-1-course-inquiry-dashboard.md) — Part 5 (Troubleshooting) requirement source
* [Program.cs](../../backend/Program.cs) — error middleware (line 44), `AddProblemDetails` (line 15), `MigrateAsync` (line 63)
* [InquiryService.cs](../../backend/Services/InquiryService.cs) — CRM outcome logging (EventIds 1–2), create path needing the audit entry
* [SimulatedCrmClient.cs](../../backend/Services/SimulatedCrmClient.cs) — CRM attempt/outcome logging (EventIds 10–14)
* [contracts.md](../../docs/architecture/contracts.md) — C3 (HTTP results/errors), C5 (commit boundary), C6 (CRM retry, timeout, privacy)
* [database/database.sql](../../database/database.sql) — SQL Server report queries to port to SQLite in the runbook
* [A11Y-101](A11Y-101-frontend-aoda-compliance.md) — GAP-5 owns the live-region announcement for filter changes
* Test fixtures: [LogCaptureProvider.cs](../../tests/CourseInquiryDashboard.Tests/Fixtures/LogCaptureProvider.cs), [InquiryApplicationFactory.cs](../../tests/CourseInquiryDashboard.Tests/Fixtures/InquiryApplicationFactory.cs)
