# Backend

The backend is a single ASP.NET Core (.NET 10) process
([ADR-0002](../architecture/adr/0002-dotnet-10-aspnet-core.md)) hosting the Web API,
the Razor dashboard shell, and Swagger. It is organized as a **layered** app —
thin controllers → an application service → EF Core / a CRM port — with request and
response DTOs separating the wire contract from the entity
([ADR-0005](../architecture/adr/0005-layered-service-dto.md)).

This document covers the *code surface*: what each layer does and where it lives.
The externally observable *contracts* (exact validation, status codes, paging,
retry timings) are specified once in
[boundary contracts C1–C8](../architecture/contracts.md) and the
[domain model](../domain/course-inquiry.md) — this doc links to them rather than
restating them.

## Request pipeline ([`Program.cs`](../../backend/Program.cs))

Registered in order:

- **Controllers + JSON**: a global [`StatusJsonConverter`](../../backend/Serialization/StatusJsonConverter.cs)
  serializes `Status` as its canonical name and accepts only the five defined names
  on input (C2).
- **Razor Pages** for the dashboard shell; **ProblemDetails**; **Swagger** with the
  `Status` enum mapped to a string schema with canonical values.
- **DI**: `TimeProvider.System` (singleton), `IInquiryService` (scoped),
  `ICrmClient` → `SimulatedCrmClient` (singleton), `AppDbContext` on SQLite from
  `ConnectionStrings:DefaultConnection` (throws if unset).
- **Logging privacy**: `Microsoft.EntityFrameworkCore` is filtered to `LogLevel.Critical`
  so EF's raw provider exceptions (with SQL and parameters) never reach a sink (C6).
- **Sanitized error middleware**: catches unhandled exceptions, logs only the
  `ErrorType`, and returns a bare `500 ProblemDetails` in **both** Development and
  Production — the developer exception page is intentionally not used (C3).
- **Startup migration**: `Database.MigrateAsync()` runs before the app serves;
  failure is fatal (C8).
- Swagger UI is mapped **Development-only**; static files serve the compiled island;
  `/` redirects to `/dashboard`.

## API surface

Base route `/api/inquiries` —
[`InquiriesController`](../../backend/Controllers/InquiriesController.cs), a thin
controller that maps HTTP to `IInquiryService` and owns only status-code decisions.
Request/response shapes and error rules are contracts
[C1](../architecture/contracts.md#c1-intake-validation-and-representation)–[C4](../architecture/contracts.md#c4-listing-pagination-and-concurrent-triage).

### `POST /api/inquiries`
Create. Binds [`CreateInquiryDto`](../../backend/Models/Dtos/CreateInquiryDto.cs)
(seven visitor fields; required/length/email validated; unknown and server-owned
fields ignored). Returns **`201`** with the `InquiryResponse` and a
`Location: /api/inquiries/{id}` header, or **`400`** for an invalid body. Server
forces `Status.New` and both timestamps; a CRM failure after commit is **not** an
HTTP error.

### `GET /api/inquiries`
List. Binds [`ListInquiriesQueryDto`](../../backend/Models/Dtos/ListInquiriesQueryDto.cs):
optional `status` filter and `page` / `pageSize` (1–100) / `sort`
(`createdDateDesc` | `createdDateAsc`). Supplied-but-blank values (`?page=`,
`?status=`) are **rejected**, not silently defaulted. Returns **`200`** with the
page envelope `{ items, page, pageSize, totalCount }` (`totalCount` is the filtered
count before paging), or **`400`** for an invalid query.

### `GET /api/inquiries/{id}`
Read one. `{id:int}` route constraint; `id <= 0` short-circuits to **`404`**.
Returns **`200`** `InquiryResponse` or **`404`**.

### `PUT /api/inquiries/{id}/status`
Update status. Binds [`UpdateStatusDto`](../../backend/Models/Dtos/UpdateStatusDto.cs)
(a **required nullable** `Status?`, so an omitted/null body fails validation instead
of defaulting to `New`). Returns **`200`** with the updated (or unchanged)
inquiry, **`400`** for an invalid body/status, or **`404`** for a missing row.
Validation runs before lookup, so an invalid status aimed at a missing id is `400`.

### `DELETE /api/inquiries/{id}`
Hard delete ([ADR-0006](../architecture/adr/0006-hard-delete.md)). Returns
**`204`** (empty body) or **`404`** — including a second delete of the same id.

**Errors:** binding/validation failures return `application/problem+json`
`ValidationProblemDetails` with an `errors` dictionary; other failures return
`ProblemDetails`. Routing produces `404`/`405`/`415` where applicable
([C3](../architecture/contracts.md#c3-http-results-and-errors)).

## Application service

[`InquiryService`](../../backend/Services/InquiryService.cs) (behind
[`IInquiryService`](../../backend/Services/IInquiryService.cs)) owns the business
rules and talks to `AppDbContext` directly:

- **Create** — observes cancellation *before* writing; samples the injected
  `TimeProvider` **once** for both `CreatedDate` and `UpdatedDate`; forces
  `Status.New`; awaits `SaveChangesAsync`; then awaits the CRM sync inside a
  post-commit isolation boundary.
- **List** — applies the status filter **before** the count and the page queries
  (two separate queries), orders by `CreatedDate` then `Id` in the requested
  direction (deterministic tie-break), and returns the envelope.
- **UpdateStatus** — defends the enum membership (throws for callers that bypass
  HTTP); a **same-status update is a no-op** that leaves `UpdatedDate` unchanged; an
  actual change re-samples UTC; a `DbUpdateConcurrencyException` (row vanished
  between lookup and write) maps to `null` → `404`, never resurrecting the row.
- **Delete** — `ExecuteDeleteAsync`; returns whether a row was removed.

The **persist-first / sync-second** boundary is the load-bearing rule: the inquiry
is the record of truth. Database failures propagate; CRM failures (including
timeout and cancellation) are caught only around the post-commit call and logged as
a safe outcome — the stored row is never rolled back or replayed
([C5](../architecture/contracts.md#c5-commit-boundary-cancellation-and-crm-delivery)).

## Persistence

- **Entity** [`CourseInquiry`](../../backend/Models/CourseInquiry.cs) and the
  [`Status`](../../backend/Models/Status.cs) enum (`New=0`, Contacted, Pending,
  Registered, Closed).
- **Mapping** [`AppDbContext`](../../backend/Models/AppDbContext.cs): required vs.
  nullable columns and C1 max-lengths; a named CHECK constraint
  (`CK_CourseInquiries_Status`) restricting stored status names to the five values;
  `EnumToStringConverter` for `Status`; and a UTC `DateTime` converter that
  re-tags provider values as UTC on read (SQLite stores no kind).
- **Schema source**: EF migrations under `backend/Migrations/`, applied at startup —
  **not** `EnsureCreated`. SQLite does not enforce `HasMaxLength`, so DTO validation
  is what enforces the length limits.
- **SQL Server companion**: [`database/database.sql`](../../database/database.sql) is
  a separate SQL Server deliverable (schema + six synthetic samples + the three
  report queries), verified on SQL Server via the `SqlServer` test lane. The runtime
  never executes it ([ADR-0003](../architecture/adr/0003-efcore-sqlite-and-sql-script.md),
  [C8](../architecture/contracts.md#c8-persistence-migration-and-sql-deliverable)).

## CRM integration

[`SimulatedCrmClient`](../../backend/Services/SimulatedCrmClient.cs) implements the
[`ICrmClient`](../../backend/Services/ICrmClient.cs) port as a **deterministic
in-process simulation** (no endpoint or credentials): completion means success, an
exception means failure. A **real Polly v8 pipeline** wraps the simulated attempt
([ADR-0007](../architecture/adr/0007-crm-port-retry-logging.md),
[C6](../architecture/contracts.md#c6-crm-retry-timeout-and-privacy)):

- Outer **2 s** total budget (not retried) → **retry** (3 retries after the original
  = at most 4 attempts; exponential 100/200/400 ms, no jitter; transient-only:
  `HttpRequestException` / `TimeoutRejectedException`) → cooperative **500 ms**
  per-attempt timeout.
- Every attempt and the terminal outcome (`success` / `failed` / `timedOut` /
  `cancelled`) are logged via source-generated `ILogger` messages carrying only the
  inquiry id, attempt number, outcome, and error *type* — **never** visitor fields or
  raw exceptions.

## Related

- [Frontend docs](../frontend/README.md) · [Domain model](../domain/course-inquiry.md)
- [Boundary contracts](../architecture/contracts.md) · [ADRs](../architecture/adr/)
- [Testing plan & evidence](../testing/tdd-plan.md)
- Security and organization rationale: [written answers](../../written-answers.md).
