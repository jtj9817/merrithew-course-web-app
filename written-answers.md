# Written Answers

Concise, reasoning-first answers to the four assessment questions. They describe
this codebase as built; the "why" behind most choices is recorded in the
[ADRs](docs/architecture/adr/) and [boundary contracts](docs/architecture/contracts.md).

---

## Troubleshooting

*Staff report that some inquiries submitted through the website don't appear in the
admin list.* I'd work the submission path end to end — visitor form → API validation
→ service → **database commit** → CRM sync
([submit flow](docs/architecture/flow--flow-submit.html)) — because a "missing"
inquiry was either never stored, stored but hidden by the view, or stored somewhere
other than the list is reading.

**A key fact that narrows this fast:** the inquiry is committed to the database
*before* the CRM sync, and a CRM failure can never prevent or undo storage
([C5](docs/architecture/contracts.md#c5-commit-boundary-cancellation-and-crm-delivery)).
So a CRM/integration problem is almost never the cause of a *missing* inquiry — it
would only affect the downstream CRM. That rules out a whole layer early.

**What I'd check, by layer:**

- **Frontend / display first (cheapest).** Is a **status filter** active? The list
  defaults to all statuses but staff often filter — a `New` inquiry won't show under
  a `Registered` filter. Also check paging (it may be on another page) and force a
  refresh, since the list is a paged snapshot, not live
  ([C4](docs/architecture/contracts.md#c4-listing-pagination-and-concurrent-triage)).
- **The submission itself (client + API boundary).** Reproduce a submit with the
  browser network panel open. Did `POST /api/inquiries` return **`201`**, **`400`**,
  or **`5xx`/nothing**? A `400` means validation rejected it (bad email format,
  missing required field, over-length) — it was never stored, and the form should be
  surfacing that error. A `5xx` or no response points at the backend/database.
- **Backend logs.** Check the app's `ILogger` output: every `/api/inquiries`
  request logs one terminal outcome entry (status code + outcome, EventIds
  20–22), rejected submissions log `validationRejected` with the failing field
  names (EventId 23), stored inquiries log `Inquiry {id} created` (EventId 3),
  and a CRM failure after commit logs an isolated outcome warning — all for one
  request sharing a `correlationId` you can quote from any 400/500 response's
  `traceId` extension ([runbook](docs/runbooks/missing-inquiry.md)). The
  sanitized unhandled-exception entries (`ErrorType`) from the error middleware
  ([Program.cs](backend/Program.cs)) cover the 5xx path.
- **Database / configuration.** Confirm the app is pointed at the expected store —
  a wrong `ConnectionStrings__DefaultConnection` (or a different `inquiries.db` file
  per environment) makes rows "vanish" because the list reads a different database.
  Confirm migrations applied (startup is fatal if they didn't).

**SQL I'd run** (the queries shipped in [`database/database.sql`](database/database.sql)):

- **Count by status** — reconcile the totals against what staff see; a large `New`
  count under an active filter usually *is* the "bug".
- **Last 7 days** — confirm recent submissions actually landed and when.
- **Duplicate email** — if a visitor resubmitted after a timeout, intake isn't
  idempotent, so you may find near-duplicates rather than a missing row
  ([C5](docs/architecture/contracts.md#c5-commit-boundary-cancellation-and-crm-delivery)).
- A direct lookup by the reported email/name to settle *stored-but-hidden* vs.
  *never-stored*: `SELECT * FROM CourseInquiries WHERE Email = @email;`

**Communicating to non-technical stakeholders:** lead with impact, not mechanism —
"we've confirmed *N* inquiries from the last 7 days are stored and none are lost;
they were hidden by a status filter" vs. "we're still confirming whether they
reached our system." State what's confirmed, what's still open, and when the next
update comes; avoid jargon; and if data integrity is in question, say so plainly.

**Preventing recurrence:** an end-to-end test covering submit → list (covered by
`IT-API`/`IT-APP` integration tests, plus observability coverage of the submit →
log → list chain — a real visitor form remains future work, permitted by Part 3);
monitoring/alerting fed by the implemented `/health` readiness endpoint and the
`CourseInquiryDashboard` meter counters (`intake_requests` by outcome, CRM
outcomes and retries); the active filter state is now obvious in the UI ("Showing
N of M · filtered by X" with a one-click Clear filter) so a filtered view isn't
mistaken for missing data; validation rejections and server errors are logged
with a correlation ID, so the "never stored" branch is provable after the fact;
and a periodic reconciliation using the count-by-status / last-7-days reports
([runbook with runnable SQLite queries](docs/runbooks/missing-inquiry.md)).
Surfacing validation errors on the submitting form itself stays future work
until a public form exists.

---

## Security

- **Input validation.** Request DTOs use DataAnnotations — `[Required]`,
  `[StringLength]`, `[EmailAddress]`
  ([CreateInquiryDto](backend/Models/Dtos/CreateInquiryDto.cs)) — and invalid input
  becomes a `400 ProblemDetails` at model binding. Status is validated **name-only**
  against the five defined values, rejecting numeric, composite, or unknown inputs
  ([StatusNames](backend/Serialization/StatusNames.cs)); list-query parameters are
  validated with bounds and overflow checks
  ([ListInquiriesQueryDto](backend/Models/Dtos/ListInquiriesQueryDto.cs)). See
  [C1–C3](docs/architecture/contracts.md#c1-intake-validation-and-representation).
- **Over-posting.** The create DTO exposes only the seven visitor fields; client-set
  `id`, `status`, and timestamps are ignored, and the service *forces* `Status.New`
  and server-assigned UTC timestamps ([InquiryService](backend/Services/InquiryService.cs)).
- **SQL injection.** All data access is EF Core LINQ / `ExecuteDeleteAsync`, which
  parameterizes every query; there is no string-concatenated SQL in the runtime path.
  (`database/database.sql` is a static deliverable, not executed by the app.)
- **Authentication / authorization — the biggest gap, and out of assessment scope.**
  Staff endpoints are currently **open**; same-origin fetch and "staff" labels are
  not access control. The API must not be exposed publicly as-is — run only with
  synthetic data locally. Production needs authenticated staff, least-privilege
  policies, CSRF protection for cookie-authenticated mutations, and atomic audit
  history ([C7](docs/architecture/contracts.md#c7-web-ui-and-hosting);
  [future design](docs/architecture/future/authentication-authorization-audit.md)).
- **Error handling.** `500`s are sanitized in **both Development and Production**
  ([Program.cs](backend/Program.cs)): no stack traces, SQL, connection strings, or
  visitor values reach the client, and validation errors don't echo attempted values
  ([C3](docs/architecture/contracts.md#c3-http-results-and-errors)).
- **Logging & sensitive data.** Logs use an **allow-list** — inquiry id, attempt
  number, outcome, error *type* — and **omit all visitor fields** (name, email,
  phone, message); raw exceptions are never attached, EF Core logging is filtered to
  `Critical`, and EF sensitive-data logging is off
  ([C6](docs/architecture/contracts.md#c6-crm-retry-timeout-and-privacy)). Responses
  never serialize the EF entity or a CRM exception. In production I'd add TLS
  termination (the `https` launch profile exists), rate limiting / anti-abuse on the
  public form and `POST`, and standard security headers.

---

## Accessibility

Three considerations applied across the Razor host and React island
([C7](docs/architecture/contracts.md#c7-web-ui-and-hosting); comprehensive outline in
[`docs/frontend/accessibility.md`](docs/frontend/accessibility.md)):

1. **Semantic structure, data table architecture, and labelled controls (WCAG 1.3.1, 4.1.2).**
   The queue is a real `<table>` with an explicit programmatic caption (`<caption className="sr-only">Incoming Course Inquiries Queue</caption>`),
   `<th scope="col">` column headers, and dynamic sort direction indication (`aria-sort="ascending" | "descending"` on the Created date header).
   Every interactive control carries an unambiguous accessible name — filter and sort dropdowns use `<label htmlFor>`
   ([Toolbar](frontend/src/components/Toolbar.tsx)), and row controls use disambiguated labels like *"Status for {name}"*,
   *"Apply for {name}"*, and *"Details for {name}"* ([InquiryTable](frontend/src/components/InquiryTable.tsx)) so screen-reader users
   can distinguish controls across rows.
2. **Keyboard operability, focus management, and bypass navigation (WCAG 2.1.1, 2.1.2, 2.4.1, 2.4.3, 2.4.7).**
   A top-of-body Skip Navigation link (`<a href="#inquiry-queue" class="skip-link">`) allows keyboard users to bypass header/appbar/toolbar
   controls directly to a persistent queue container (`<section id="inquiry-queue" tabindex="-1">`) across all queue states (loading, empty, error, ready).
   All interactive actions are native keyboard-operable elements with high-contrast `:focus-visible` indicators (3:1 contrast ratio).
   The detail modal drawer traps focus (`Tab`/`Shift+Tab`), sets the background `inert`, locks body scroll, closes on `Escape`,
   and safely **returns focus to the opener button** on close — with a graceful fallback to `#inquiry-queue` or `#status-filter`
   if the row was concurrently removed ([DetailPanel](frontend/src/components/DetailPanel.tsx), [App](frontend/src/App.tsx)).
3. **Perceivable asynchronous feedback, error association, and non-color-dependent communication (WCAG 1.4.1, 3.3.1, 4.1.3).**
   Asynchronous transitions announce through dual live regions ([LiveRegions](frontend/src/components/LiveRegions.tsx)):
   a polite region (`role="status"`) for save confirmations, filter updates, and pagination transitions (announcing on-page count e.g. *"Page 2 (of 3) loaded, showing 20 matching inquiries"* alongside `aria-current="page"` on the active page indicator);
   and an assertive region (`role="alert"`) for mutation errors and missing records. Failed status mutations associate inline `aria-invalid="true"`
   on the row's `<select>`. Workflow status is never communicated by color alone: badges display the text **name**, supplemented in Colorblind Mode
   by distinct shape and icon cues (solid pill + star, dashed + chat, dotted + clock, double border + check, slash).
---

## Code quality

The solution is organized as a **layered, single-deployable** app, chosen to keep
business rules in one testable place without over-engineering a small CRUD tool
([ADR-0005](docs/architecture/adr/0005-layered-service-dto.md)):

- **Thin controllers → service → EF Core.** `InquiriesController` only handles HTTP
  (routing, binding, status codes) and delegates to `IInquiryService`, which owns
  every business rule (forced status/timestamps, no-op updates, deterministic
  filter/paging, hard delete, and the persist-first/sync-second CRM boundary).
  `AppDbContext` is used directly — EF Core already is a unit-of-work + repository, so
  no repository layer was added.
- **DTOs separate the wire contract from the entity.** `CreateInquiryDto` /
  `UpdateStatusDto` / `InquiryResponse` prevent over-posting and keep the API stable;
  the EF entity is never serialized.
- **The CRM is behind a port** (`ICrmClient`) so the service depends on an
  abstraction, and time is injected via `TimeProvider` — both make failure and
  timing paths deterministically testable
  ([ADR-0007](docs/architecture/adr/0007-crm-port-retry-logging.md)).
- **Clear folders.** Backend: `Controllers/`, `Services/`, `Models/` + `Models/Dtos/`,
  `Serialization/`, `Hosting/`, `Pages/`. Frontend: pure logic in `src/lib/`
  (query/state/paging/outcome helpers, unit-tested in isolation) kept separate from
  presentational `src/components/`.
- **Decisions and edge cases are written down** as ADRs and boundary contracts, and
  the behavior was built **test-first** (red → green → refactor) with xUnit and
  Vitest, so the "why" and the guardrails outlive the code
  ([ADR-0009](docs/architecture/adr/0009-testable-boundary-contracts.md)).
