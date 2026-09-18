# Written Answers

Reasoning-first answers to the four assessment questions, describing the codebase
as built. The deeper "why" behind each choice is recorded in the
[ADRs](docs/architecture/adr/) and [boundary contracts](docs/architecture/contracts.md).

---

## Troubleshooting

**Problem.** A staff member reports that some inquiries submitted through the
website do not appear in the admin list. It is not yet known whether those
inquiries were lost or merely not displayed.

**Assumptions.**

- The intake path is `visitor form → POST /api/inquiries → validation →
  InquiryService → database commit → best-effort CRM sync`
  ([submit flow](docs/architecture/flow--flow-submit.html)); the dashboard list
  reads the same database through a paged `GET /api/inquiries` with an optional
  status filter.
- The system commits **first** and syncs to the CRM **second**: a CRM failure is
  caught after the commit and can never block or undo storage
  ([C5](docs/architecture/contracts.md#c5-commit-boundary-cancellation-and-crm-delivery)).
  The CRM is therefore an unlikely cause of a *missing* inquiry, which rules out
  the integration layer early.
- "Missing" can only mean one of three things: the inquiry was **never stored**
  (the request failed or was rejected), is **stored but hidden** (active filter,
  paging), or is **stored where the list isn't reading** (wrong
  database/configuration).

With that mental model, I'd work the path from the cheapest end, collecting
evidence at each step before moving a layer deeper.

**1. Scope the report.** One visitor or several? Which timeframe? Get a concrete
example (email + approximate submit time). A one-off points at validation or a
transient error; a cluster points at configuration or the backend.

**2. Rule out the display layer (cheapest, most common).** Is a **status
filter** active? A `New` inquiry will not appear under a `Registered` filter.
Also check paging (the row may simply be on another page) and force a refresh,
since the list is a paged snapshot, not a live view
([C4](docs/architecture/contracts.md#c4-listing-pagination-and-concurrent-triage)).

**3. Reproduce the submission and read the HTTP outcome.** Submit an inquiry
with the browser network panel open; the status of `POST /api/inquiries`
splits the investigation cleanly:

- **`201`** → the inquiry is stored; the defect is in the view or
  configuration. Skip to step 5.
- **`400`** → validation rejected it (invalid email, missing required field,
  over-length value). It was never stored *by design*; the follow-up question
  is whether the visitor saw and could act on the error.
- **`5xx` or no response** → backend or database fault. Continue to step 4.

**4. Backend logs, correlated per request.** Every request logs exactly one
terminal-outcome entry (EventIds 20-22): rejections name the failing fields
(EventId 23), successful writes log `Inquiry {id} created` (EventId 3), and a
post-commit CRM failure logs an isolated warning. All entries for a request
share a `correlationId`, obtainable from any 400/500 response's `traceId`
extension, so a specific report can be traced end to end
([runbook](docs/runbooks/missing-inquiry.md)). Sanitized unhandled-exception
entries from the error middleware cover the 5xx path.

**5. Database and configuration.** Confirm the app points at the expected
store: a wrong `ConnectionStrings__DefaultConnection` (or a different
`inquiries.db` per environment) makes rows "vanish" because the list reads a
different database. Migrations run at startup and startup fails loudly if they
cannot, so a missing table is unlikely.

**SQL I'd run** (shipped in [`database/database.sql`](database/database.sql)):

- **Count by status:** reconcile totals against what staff see; a large `New`
  count behind an active filter usually *is* the reported "bug."
- **Last 7 days:** confirm recent submissions actually landed, and when.
- **Duplicates by email:** intake is not idempotent, so a visitor who
  resubmitted after a timeout may have produced near-duplicates rather than one
  missing row.
- **Direct lookup** to settle stored-but-hidden vs. never-stored:
  `SELECT * FROM CourseInquiries WHERE Email = @email;`

**Communicating to non-technical stakeholders.** Lead with impact and facts,
not mechanism: "I've confirmed that all inquiries from the last 7 days were
stored and none were lost. The problem was simply in the filters on the
dashboard." That lands better than "we're still confirming whether they reached
us." State what is confirmed, what is still open, and when the next update
comes; if data integrity is ever in question, say so plainly.

**Preventing recurrence.**

- *Detection:* alerting fed by the `/health` readiness endpoint and the
  `CourseInquiryDashboard` meters (intake outcomes, CRM outcomes and retries).
- *Forensics:* every rejection and server error is logged with a correlation
  ID, so the "never stored" branch is provable after the fact.
- *UI clarity:* the active filter is explicit ("Showing N of M · filtered by X"
  with one-click Clear), so a filtered view can't be mistaken for missing data.
- *Regression safety:* integration tests cover submit → list, and a periodic
  reconciliation runs the count-by-status and last-7-days reports
  ([runbook](docs/runbooks/missing-inquiry.md)).

---

## Security

- **Input validation.** DTOs enforce `[Required]`, `[StringLength]`, and
  `[EmailAddress]`
  ([CreateInquiryDto](backend/Models/Dtos/CreateInquiryDto.cs)); invalid input
  fails at model binding into a `400 ProblemDetails`. Status binds **name-only**
  against the five known values, rejecting numeric, composite, or unknown
  tokens ([StatusNames](backend/Serialization/StatusNames.cs)), and list-query
  parameters get bounds and overflow checks
  ([ListInquiriesQueryDto](backend/Models/Dtos/ListInquiriesQueryDto.cs)). See
  [C1-C3](docs/architecture/contracts.md#c1-intake-validation-and-representation).
- **Over-posting.** The create DTO exposes only the seven visitor fields;
  `id`, `status`, and timestamps are server-controlled: the service forces
  `Status.New` and assigns UTC timestamps
  ([InquiryService](backend/Services/InquiryService.cs)).
- **SQL injection.** All data access goes through EF Core LINQ /
  `ExecuteDeleteAsync`, which parameterizes every query; no string-built SQL
  exists in the runtime path. (`database/database.sql` is a static deliverable,
  never executed by the app.)
- **Authentication/authorization (the biggest gap, and out of assessment
  scope).** Staff endpoints are currently **open**: same-origin fetch and "staff"
  labeling are not access control, so the API must not be exposed publicly
  as-is. Production requires authenticated staff identities, least-privilege
  policies, CSRF protection for cookie-authenticated mutations, and an audit
  trail ([C7](docs/architecture/contracts.md#c7-web-ui-and-hosting);
  [future design](docs/architecture/future/authentication-authorization-audit.md)).
- **Error handling.** `500` responses are sanitized in **both** Development and
  Production ([Program.cs](backend/Program.cs)): no stack traces, SQL,
  connection strings, or visitor values reach the client, and validation errors
  never echo the attempted input
  ([C3](docs/architecture/contracts.md#c3-http-results-and-errors)).
- **Logging and sensitive data.** Logging is allow-listed (inquiry id, attempt
  number, outcome, and error *type* only); all visitor fields (name, email,
  phone, message) are omitted, raw exceptions are never attached, and EF Core
  sensitive-data logging is off
  ([C6](docs/architecture/contracts.md#c6-crm-retry-timeout-and-privacy)).
  Responses never serialize the EF entity or a CRM exception. In production I
  would add TLS termination, rate limiting on the public form and `POST`, and
  standard security headers.

---

## Accessibility

Three considerations applied across the Razor host and React island (full
outline in [`docs/frontend/accessibility.md`](docs/frontend/accessibility.md)):

1. **Semantic structure and labelled controls (WCAG 1.3.1, 4.1.2).** The queue
   is a real `<table>` with a screen-reader-only caption, `<th scope="col">`
   headers, and `aria-sort` on the sortable Created column. Every interactive
   control has an unambiguous accessible name: dropdowns use `<label htmlFor>`,
   and per-row controls are disambiguated (*"Status for {name}"*, *"Details for
   {name}"*) so screen-reader users can tell rows apart.
2. **Keyboard operability and focus management (WCAG 2.1.1, 2.1.2, 2.4.1,
   2.4.3, 2.4.7).** A skip link bypasses the header and toolbar straight to the
   queue container; every action is a native keyboard-operable element with a
   high-contrast `:focus-visible` indicator. The detail modal traps focus,
   marks the background `inert`, closes on `Escape`, and **returns focus to the
   opener button**, falling back to the queue if that row was concurrently
   removed.
3. **Perceivable async feedback, and status never conveyed by color alone
   (WCAG 1.4.1, 3.3.1, 4.1.3).** Dual live regions announce outcomes:
   politely (`role="status"`) for saves, filter, and pagination updates with
   counts, and assertively (`role="alert"`) for errors and missing records. A
   failed status mutation marks the row's `<select>` with `aria-invalid`.
   Workflow badges always show the status text; Colorblind Mode layers on
   per-status shapes and icons and re-hues the olive canvas to a CVD-safe soft
   blue, keeping every contrast ratio unchanged.

---

## Code quality

Organized as a **layered single deployable**, keeping business rules in one
testable place without over-engineering a small CRUD tool
([ADR-0005](docs/architecture/adr/0005-layered-service-dto.md)):

- **Thin controllers → service → EF Core.** `InquiriesController` handles only
  HTTP semantics (routing, binding, status codes); `IInquiryService` owns every
  business rule (forced status and timestamps, no-op update handling,
  deterministic filtering/paging, hard delete, and the persist-first/
  sync-second CRM boundary). `AppDbContext` is used directly because EF Core
  already is a unit-of-work + repository; adding one would be redundant
  abstraction.
- **DTOs isolate the wire contract from the entity.** `CreateInquiryDto`,
  `UpdateStatusDto`, and `InquiryResponse` prevent over-posting and keep the
  API stable; the EF entity is never serialized.
- **External concerns sit behind ports.** The CRM is behind `ICrmClient` and
  time behind `TimeProvider`, so failure and timing paths are deterministically
  testable ([ADR-0007](docs/architecture/adr/0007-crm-port-retry-logging.md)).
- **Logic separated from presentation.** Backend folders mirror the layers
  (`Controllers/`, `Services/`, `Models/Dtos/`, `Serialization/`, `Hosting/`);
  frontend pure logic lives in unit-tested `src/lib/` helpers, apart from
  presentational `src/components/`.
- **Decisions and tests outlive the code.** ADRs and boundary contracts record
  the "why"; behavior was built test-first (red → green → refactor) with xUnit
  and Vitest ([ADR-0009](docs/architecture/adr/0009-testable-boundary-contracts.md)).
