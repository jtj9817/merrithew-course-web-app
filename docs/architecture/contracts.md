# Boundary contracts and edge cases

> **Implemented and verified through TODO Phase 9.** This document closes
> unspecified behavior in the [assessment](../../option-1-course-inquiry-dashboard.md) and existing
> ADRs. Numeric limits, wire shapes, retry timings, and no-op semantics below are
> **project decisions**, not requirements quoted from the assessment.
> [ADR-0009](adr/0009-testable-boundary-contracts.md) records this refinement.
> The [domain specification](../domain/course-inquiry.md) owns business meaning;
> this file owns externally observable technical contracts. The architecture
> [model](model.json) owns REQ/VER identifiers and allocation, not duplicate API
> schemas. The [TDD plan](../testing/tdd-plan.md), case catalogs, and Phase 9
> execution record provide the passing evidence for these contracts.

## C1. Intake validation and representation

JSON property names are camelCase. Requests use `application/json`; responses
use `application/json`, except errors as specified in C3. The create DTO exposes
only the seven visitor fields below. Unknown JSON properties are ignored,
including supplied `id`, `status`, `createdDate`, and `updatedDate`; none may
influence stored server-owned fields. A status update accepts only `status`.

| Field | Required | Maximum length (UTF-16 code units) |
| --- | --- | ---: |
| `firstName` | Yes | 100 |
| `lastName` | Yes | 100 |
| `email` | Yes, plus `[EmailAddress]` | 254 |
| `phone` | No | 50 |
| `courseName` | Yes | 200 |
| `preferredLocation` | No | 200 |
| `message` | No | 4000 |

Use explicit `[Required]` and `[StringLength]` on input DTOs. Missing, null,
empty, and whitespace-only required values fail. Optional missing/null values
remain null; optional empty strings and whitespace are accepted and preserved.
Do not trim, lowercase, truncate, or HTML-encode stored visitor text. Names,
locations, and messages support Unicode, apostrophes, and markup as **data**;
render them as text (C7). Email validation is the .NET 10 DataAnnotations format
check, not deliverability, DNS, or a stricter hand-written RFC parser.

A repeated email or identical submission creates another row with a distinct
ID. There is no uniqueness constraint, deduplication, or idempotency key.
Duplicate-email reporting in C8 is analytical, not an intake rejection rule.
If automated intake retries become part of the client contract, use explicit
[idempotency keys](future/idempotency-keys.md), never email/body heuristics.

`InquiryResponse` contains the entity's eleven documented fields, with a
positive integer `id`, canonical status name, and UTC ISO-8601 timestamps ending
in `Z`. Nullable visitor fields are present with JSON null when absent. Never
serialize an EF entity or a CRM exception/result into the public response.

## C2. Status and timestamps

The only statuses are `New`, `Contacted`, `Pending`, `Registered`, and `Closed`.
Input names are case-insensitive and tolerate surrounding whitespace; output
names use this canonical casing. Apply the same rule to the JSON status update
and the optional list filter. Reject unknown/empty names, comma-separated names,
numbers, numeric strings (including `0` and `"0"`), booleans, arrays, and objects.
Omitting the list filter means all statuses; supplying `status=` is invalid.

For updates, missing/null `status` is invalid, **not** `New`. A non-nullable enum
silently defaults to zero when omitted, and enum binding/conversion alone is
not a membership or string-only check. Use a required nullable status at the
DTO boundary, explicit name validation/conversion, and a defined-enum guard at
the service boundary. `[BindRequired]` does not enforce JSON body presence.
Invalid internal enum values must not persist even when HTTP is bypassed.

Any defined status may replace any other, including `Closed → New`. Updating
to the current status returns `200` with the existing inquiry and leaves
`updatedDate` unchanged. An actual change sets `updatedDate` from an injected
`TimeProvider`; `createdDate` never changes. Create reads UTC time once and uses
that instant for both timestamps, with status forced to `New`.

These are **wall-clock timestamps**, not versions: distinct changes may share
an instant, and a clock correction may move time backwards. Do not promise
strictly increasing timestamps or use them for conflict detection. Tests advance
controlled time when demonstrating a later update, and separately cover a
frozen/backward clock. Status updates and deletions do not initiate CRM sync.

## C3. HTTP results and errors

| Endpoint | Success | Missing resource / invalid request |
| --- | --- | --- |
| `POST /api/inquiries` | `201`, `InquiryResponse`, `Location: /api/inquiries/{id}` resolving to the created resource | Invalid DTO/body: `400`; definite database failure: sanitized `500`; CRM failure after commit is not an HTTP error |
| `GET /api/inquiries` | `200`, page envelope from C4, including empty results | Invalid query: `400` |
| `GET /api/inquiries/{id}` | `200`, `InquiryResponse` | `404` |
| `PUT /api/inquiries/{id}/status` | `200`, updated or unchanged `InquiryResponse` | Invalid body/status: `400`; valid request for missing row: `404` |
| `DELETE /api/inquiries/{id}` | `204`, empty body, permanent removal | `404`, including a second delete |

Use `{id:int}` route constraints. Zero/negative integer IDs cannot identify a
row and return `404`; non-integer and Int32-overflow paths fail routing with
`404`. Validation runs before resource lookup: an invalid status body aimed at
a missing integer ID returns `400`, not `404`. Malformed JSON, an absent body,
JSON null, and incorrect field types return `400`; unsupported request media
types return `415`. Unsupported verbs on known routes return `405`.

For API requests accepting JSON, validation/binding failures return
`application/problem+json` `ValidationProblemDetails`; other failures return
`ProblemDetails` with matching numeric `status`, a nonempty `title`, and `type`.
Validation adds an `errors` dictionary identifying the failing field or body.
Do not pin tests to framework error wording, JSON property order, or an exact
problem-type URL. Handle routing/status-code errors as well as exceptions;
exception middleware alone does not manufacture all 400/404/405/415 responses.

Sanitize 500s in **Development and Production**: no stack traces, SQL, paths,
connection strings, exception messages, or visitor values. Validation errors
must not echo attempted values. Apply the safe logging rules in C6 to exception
handling too. A disconnected request has no guaranteed HTTP response; do not
invent a required 499 response or turn cancellation into a claimed 201 (C5).

## C4. Listing, pagination, and concurrent triage

| Parameter | Omitted value | Accepted values |
| --- | --- | --- |
| `status` | All statuses, including Closed | One name per C2 |
| `page` | `1` | Int32 integer at least 1 |
| `pageSize` | `20` | Integer 1–100 |
| `sort` | `createdDateDesc` | `createdDateDesc` or `createdDateAsc` (exact spelling) |

Blank, malformed, negative/zero, and out-of-range supplied paging values return
`400`; never clamp them silently. Reject a request if `(page - 1) * pageSize`
would exceed Int32.MaxValue, computing the check without overflowing. Apply
status filtering **before** counting or paging. Sort by `CreatedDate`, then `Id`,
both in the selected direction, so equal timestamps have deterministic order.
Do not materialize all inquiries to filter/page in memory.

The response is `{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 42 }`.
`totalCount` is the filtered count before pagination. A valid page beyond the
end returns `200` with empty items and the filtered total; an empty store has
`totalCount: 0`. Counting and fetching the page are separate database operations,
not a claim that one IQueryable executes once for both results.

Offset pagination is not a snapshot across requests, or across count/page
queries under concurrent writes: inserts/deletes can shift rows or temporarily
make totals differ from the displayed page. Stable ordering prevents tie-related
flakiness, not all concurrent paging anomalies. Refresh reconciles the UI.

Status writes are **last committed write wins**, with no ETag, rowversion, or
409 conflict contract. If a row disappears between lookup and update/delete,
map the zero-row concurrency failure to `404`; never recreate it or report a
successful write. A no-op reports the row observed at lookup; it is not a lock
against a later concurrent change. No audit history is promised.
Production identity and atomic mutation history are specified separately as a
[future authentication, authorization, and audit design](future/authentication-authorization-audit.md).

## C5. Commit boundary, cancellation, and CRM delivery

1. Validate before attempting persistence.
2. Set server-owned values and await `SaveChangesAsync(requestToken)`. One
   inquiry insert is atomic. Do not put the CRM operation inside the transaction.
3. Only after a successful commit, **await**
   `ICrmClient.SyncInquiryAsync(inquiry, cancellationToken)` in the same request.
   The port returns `Task`: completion means success; failure is an exception,
   not a second structured-result protocol. There is no fire-and-forget task.
4. Catch CRM errors **only around this post-commit operation**, including
   cancellation. Keep the row and the created response; log a sanitized outcome.
   If the connection is still usable, return `201` regardless of CRM outcome.

| Event | Required outcome |
| --- | --- |
| Cancellation observed before the write starts | No insert or CRM attempt; propagate cancellation, not a fabricated success |
| Definite insert/transaction failure before commit | No partial row, no CRM call, sanitized HTTP failure if a response can be sent |
| Cancellation/disconnect while a write may be committing | Outcome may be ambiguous to the caller; do not assert that no row exists just because HTTP failed |
| Cancellation after confirmed commit | Stop/skip CRM work cooperatively, retain the row; never roll back or replay the insert |
| CRM transient/permanent/timeout/unexpected exception after commit | Row remains readable from a fresh context; no CRM exception escapes the create operation |
| Process termination after commit, before/during CRM | Row survives restart; sync can be lost, because there is no durable outbox |
| Response lost after commit; caller repeats POST | Another row may be created; exactly-once intake/delivery is not promised |

Thus “CRM failure never prevents storage” does **not** mean zero submission
latency. The awaited simulation has a bounded cooperative budget (C6). Neither
successful delivery of a response nor delivery to the CRM can be guaranteed by
a database commit alone. No background queue, automatic intake retry, or durable
CRM retry is added by this plan.
If those guarantees become business requirements, supersede this contract with
the [durable outbox](future/durable-crm-outbox.md) and
[idempotency-key](future/idempotency-keys.md) designs.

## C6. CRM retry, timeout, and privacy

Use the real Polly pipeline around a deterministic in-process simulated attempt;
no network or CRM credentials. Default simulation succeeds. Test configuration
can control outcomes without deriving failures from visitor email/message data.

- Maximum **three retries after the original attempt**: at most four attempts.
- Retry only transient `HttpRequestException` and per-attempt
  `TimeoutRejectedException`. Do not retry `OperationCanceledException`, a
  permanent simulated rejection (`InvalidOperationException`), or unknown errors.
- Exponential delays: 100, 200, 400 ms; no jitter for this local simulation.
- Per-attempt timeout: 500 ms. Outer total CRM budget: 2 seconds including
  delays. Order: total timeout → retry → attempt timeout → simulated operation.
  The outer timeout is not itself retried and may stop execution before all
  four attempts; unlike a budget above 2.7 seconds, it bounds a slow sequence
  before all attempt timeouts and backoffs are exhausted.
- Honor the **inner** cancellation token in each attempt and delay. Polly
  timeout is cooperative, not forcible thread termination; the simulation must
  respond promptly. Cancellation during backoff must not start another attempt.
- Log each started attempt and its terminal outcome, plus the final sync outcome
  (`success`, `failed`, `timedOut`, or `cancelled`) with the persisted inquiry ID.
  Exhaustion rethrows to the service's post-commit isolation boundary.

Use an allow-list of log properties: inquiry ID, attempt number, outcome,
duration, and safe error category/type. **Omit all visitor fields** rather than
trying to mask an arbitrary DTO. Never attach raw exceptions, messages, inner
exceptions, DTOs, or visitor-valued scopes to logs. Redaction of email/phone
alone is insufficient if names or message content disclose the same data.
Disable EF sensitive-data logging and request/response-body logging. Ensure
Polly telemetry/exception middleware cannot bypass this rule by logging raw
exceptions. Test rendered messages, structured state, scopes, and exception
objects at the sink, on success, retry, exhaustion, timeout, and cancellation.
This is operational logging, not a durable business audit trail.

## C7. Web UI and hosting

The React island owns list/filter/paging/detail/status updates and user feedback.
**Create and delete are API/Swagger operations**, not island features. The public
visitor form is external and out of scope. The Razor shell serves `/dashboard`
with one mount point, compiled assets, and loading/no-JavaScript guidance; it
does not provide a functional inquiry queue without JavaScript.

- Changing a filter resets page to 1. Refresh after a successful update so a row
  leaving the active filter disappears and totals reconcile. If the current page
  becomes empty above page 1, navigate to the last available page (at least 1)
  and refetch. Distinguish loading, empty store/filter, and request failure.
- Ignore stale list/detail responses after a newer selection or unmount. Use
  cancellation plus ordering protection; cancellation alone cannot undo an
  already-completed response. Older results must not overwrite newer state.
- Keep persisted state on a failed status update; no false success message.
  Disable duplicate submissions while a mutation is pending. A detail/update
  404 explains that the record is gone, clears stale detail, and refreshes the
  list. A successful write followed by a failed refresh is reported as “saved,
  refresh failed,” not “save failed.”
- Render visitor and error text safely, without HTML injection. Display safe
  ProblemDetails validation/general errors; network failures or non-JSON error
  responses get a generic recoverable message, not a parsing crash or raw HTML.
- Label controls, use semantic table headings and keyboard-operable actions,
  announce success/errors via live regions, and manage detail-panel focus
  (including return to its opener). Do not depend on color alone.

Vite emits production assets into a dedicated `backend/wwwroot` asset subfolder
so cleaning a build does not delete unrelated static files. Razor resolves the
production entry and its CSS/imports through the Vite manifest; do not hard-code
hashed names or require the Vite dev server at runtime. Host integration tests
request the actual built assets; a real-browser walkthrough proves mounting and
interaction. Component tests and TestServer cannot establish visual correctness.

Authentication/authorization remain out of assessment scope. Staff labels,
same-origin fetch, and lack of CORS are **not access control**. Use only synthetic
data and a local demo environment; exposing the open API leaks personal data
and permits destructive operations. Production auth, CSRF strategy for any
future cookie-authenticated API, retention, and audit are future design work,
not implemented guarantees or invented 401/403 acceptance tests.
The conditional production design is documented in
[authentication, authorization, and audit history](future/authentication-authorization-audit.md).

## C8. Persistence, migration, and SQL deliverable

Runtime SQLite schema comes from EF migrations, not `EnsureCreated` alongside
migrations. One host applies migrations before serving requests. Startup failure
is fatal, not a fallback to an empty/in-memory store. Test an empty file and a
restart of the same file: existing rows survive and startup is repeatable. This
is a single-instance assessment deployment, not a multi-host migration strategy.

Store status names as strings constrained to the five C2 values. Required columns
are NOT NULL; optional columns are nullable; email is not unique. Keep the C1
length mapping aligned with SQL Server `nvarchar` lengths, but do not assume
SQLite enforces `HasMaxLength`: request validation enforces these limits.
Use UTC `DateTime` throughout and restore UTC kind when reading values whose
provider representation does not preserve it; verify reloaded JSON retains `Z`
without shifting the instant. Do not use SQL Server `rowversion` in SQLite.

`database/database.sql` is a separate **SQL Server** deliverable, not the SQLite
bootstrap script. It must execute in an empty disposable SQL Server database,
create the corresponding table with int identity and UTC `datetime2(7)` fields,
and insert at least five synthetic samples. Re-running it against an existing
table need not succeed; it must not silently drop real data. The runtime does
not execute it or require SQL Server. Verify its DDL and queries on SQL Server,
not on SQLite or by searching SQL source text.

The three queries have these semantics:

1. **Last seven days:** capture one UTC `@AsOf` instant (default
   `SYSUTCDATETIME()`); include `CreatedDate >= DATEADD(day, -7, @AsOf)` and
   `CreatedDate <= @AsOf`. Exclude older and future rows. This is a rolling
   seven-day interval, not calendar dates or local time. Allow the test harness
   to supply a fixed `@AsOf` to the same query rather than copy its logic.
2. **Count by status:** group stored rows by status, including `Closed`; only
   represented statuses produce groups. Hard-deleted rows do not count.
3. **Duplicate email:** group by `LOWER(LTRIM(RTRIM(Email)))`, return groups with
   `COUNT(*) > 1` and their counts. This chosen reporting normalization does not
   modify stored values or impose email uniqueness. Specify collation in the
   script if comparisons would otherwise depend on the test database default.

## Reference basis

Framework mechanics were checked against primary documentation; project-specific
choices above are deliberately separate from these sources:

- Microsoft Learn: [MVC validation and missing values](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation?view=aspnetcore-10.0), [model binding](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/model-binding?view=aspnetcore-10.0).
- Microsoft Learn: [ASP.NET Core integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0), [EF testing strategy](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy), [SQLite test connection lifetime](https://learn.microsoft.com/en-us/ef/core/testing/testing-without-the-database#sqlite-in-memory).
- Microsoft Learn: [EF transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions), [concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency), [entity property limits](https://learn.microsoft.com/en-us/ef/core/modeling/entity-properties#maximum-length), [UTC DateTime conversion](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions#specify-the-datetimekind-when-reading-dates).
- Polly: [retry semantics](https://www.pollydocs.org/strategies/retry.html), [cooperative timeout semantics](https://www.pollydocs.org/strategies/timeout.html).
