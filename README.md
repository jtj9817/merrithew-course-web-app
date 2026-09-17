# Merrithew Course Inquiry Dashboard

An internal tool for Merrithew staff to triage course-registration inquiries
submitted from the website: view the queue, filter by status, inspect details,
and update an inquiry's workflow status. Each new inquiry is durably stored and
then pushed to a **simulated** CRM on a best-effort basis. It runs as a single
ASP.NET Core process — a Web API, a Razor dashboard shell, and a compiled React
island — over a local SQLite file, with no external services to stand up.

Built as the ".NET / C# Web Developer" technical assessment
([full brief](option-1-course-inquiry-dashboard.md)).

## Requirements

- **.NET 10 SDK** — the solution targets `net10.0` and [`global.json`](global.json)
  pins the `10.0.1xx` SDK band. This is the one hard prerequisite to build and run
  the app ([ADR-0002](docs/architecture/adr/0002-dotnet-10-aspnet-core.md)).
- **A current Node.js LTS (20 or 22) with [pnpm](https://pnpm.io)** — only to build
  the React island (Vite 8 requires a current Node LTS). The repo ships a
  `pnpm-lock.yaml`; npm or yarn work too, but pnpm matches the lockfile.
- No database engine. The runtime store is a SQLite file created automatically at
  startup ([ADR-0003](docs/architecture/adr/0003-efcore-sqlite-and-sql-script.md)).

## Setup and run

The React island compiles into `backend/wwwroot/app/`, which is **git-ignored** —
a fresh checkout has no built assets, so build the frontend before running, or the
dashboard degrades to its "JavaScript required" guidance (the API and Swagger stay
fully functional either way).

```bash
# 1. Restore .NET dependencies (and the dotnet-ef local tool, used for migrations)
dotnet restore
dotnet tool restore

# 2. Build the React island -> backend/wwwroot/app/ (with a Vite manifest)
cd frontend
pnpm install
pnpm build            # runs: tsc -b && vite build
cd ..

# 3. Run the app (applies EF migrations, then serves)
dotnet run --project backend
```

The HTTP profile listens at **`http://localhost:5083`**
([launchSettings.json](backend/Properties/launchSettings.json)):

| Path | What |
| --- | --- |
| `/` | Redirects to `/dashboard`. |
| `/dashboard` | Staff triage UI (the React island; needs the frontend build). |
| `/swagger` | OpenAPI UI — **Development only** — for create/delete and manual calls. |
| `/api/inquiries` | The Web API (below). |

Startup runs `Database.MigrateAsync()` before listening, creating
`backend/inquiries.db` on first run; a migration failure is **fatal** (no in-memory
fallback). Override the connection string with the
`ConnectionStrings__DefaultConnection` environment variable.

```bash
# Authoring new migrations (not needed to run — startup applies existing ones):
dotnet ef migrations add <Name> --project backend
dotnet ef database update --project backend
```

## API

Base route `/api/inquiries`
([`InquiriesController`](backend/Controllers/InquiriesController.cs)). Full request,
response, and error semantics are specified in
[contracts C1–C4](docs/architecture/contracts.md) and documented in
[`docs/backend/`](docs/backend/README.md).

| Endpoint | Purpose | Success | Errors |
| --- | --- | --- | --- |
| `POST /api/inquiries` | Create an inquiry | `201` + `Location` | `400` invalid body |
| `GET /api/inquiries` | List (optional `status` filter, `page`/`pageSize`/`sort`) | `200` page envelope | `400` invalid query |
| `GET /api/inquiries/{id}` | Read one | `200` | `404` |
| `PUT /api/inquiries/{id}/status` | Update status | `200` | `400` / `404` |
| `DELETE /api/inquiries/{id}` | Permanently delete | `204` | `404` |

Errors use RFC 7807 `ProblemDetails`; `500`s are sanitized in both Development and
Production. Create and delete are intentionally API/Swagger operations, not
dashboard controls ([C7](docs/architecture/contracts.md#c7-web-ui-and-hosting)).

## Testing

```bash
dotnet test --filter 'Category!=SqlServer'   # unit + integration + host (no external DB)
cd frontend && pnpm test                     # Vitest component/unit suite (vitest run)
```

Both suites run offline. The **SQL Server** lane is separate and needs a disposable
server via `SQLSERVER_TEST_CONNECTION_STRING` (it creates/drops its own
`CourseInquiryTests_`-prefixed databases and never touches a supplied one; missing
infrastructure fails as *Blocked* rather than silently skipping):

```bash
dotnet test --filter 'Category=SqlServer'
```

The latest full-suite results are recorded in the
[Phase 7 regression gate](docs/testing/tdd-plan.md#phase-7-implementation-record):
146 non-SqlServer .NET tests, 52 Vitest tests, and 8 SQL Server tests, all passing,
plus [manual browser evidence](docs/testing/manual-evidence.md).

## Project layout

```text
backend/     ASP.NET Core host: Controllers/, Services/, Models/ + Models/Dtos/,
             Serialization/, Migrations/, Pages/ (Razor shell), Hosting/ (Vite manifest)
frontend/    React + TypeScript island (Vite); builds into backend/wwwroot/app/
database/    database.sql — SQL Server companion deliverable (schema, samples, reports)
tests/       xUnit: Unit/, Integration/, SqlServer/, and isolated Fixtures/
docs/        architecture/ (ADRs, contracts, diagrams), domain/, backend/, frontend/,
             testing/ (TDD plan, case catalogs, evidence), CHANGELOG.md
```

## Assumptions

- **No authentication or authorization.** The assessment scopes this out, so staff
  endpoints are open. Same-origin fetch and "staff" labels are **not** access
  control — run only with synthetic data in a local/demo environment
  ([C7](docs/architecture/contracts.md#c7-web-ui-and-hosting)).
- **SQLite is the runtime store**, created from EF migrations; the required
  `database/database.sql` is a separate **SQL Server** deliverable (the brief's
  stated preference), not the bootstrap script the app runs
  ([ADR-0003](docs/architecture/adr/0003-efcore-sqlite-and-sql-script.md)).
- **Single-instance deployment.** Migrations run at startup and are fatal on
  failure; this is not a multi-host migration strategy
  ([C8](docs/architecture/contracts.md#c8-persistence-migration-and-sql-deliverable)).
- **The CRM is an in-process simulation** — no real endpoint or credentials. Sync is
  awaited *after* the inquiry commits and is best-effort: a CRM failure never rolls
  back or fails the create, and there is no durable delivery guarantee
  ([ADR-0007](docs/architecture/adr/0007-crm-port-retry-logging.md)).
- **Timestamps are UTC wall-clock observations, not versions**; duplicate emails are
  allowed (the duplicate-email report is analytical, not an intake rule)
  ([C2](docs/architecture/contracts.md#c2-status-and-timestamps),
  [C8](docs/architecture/contracts.md#c8-persistence-migration-and-sql-deliverable)).

## Delete: hard delete (assessment choice)

The brief lets `DELETE` be a hard or soft delete and asks the choice to be justified
here. **`DELETE /api/inquiries/{id}` performs a hard delete** — the row is removed.

The reasoning ([ADR-0006](docs/architecture/adr/0006-hard-delete.md)): the status
vocabulary already includes **`Closed`**, which covers the "keep the record but take
it out of active work" case. A soft-delete flag plus the global query filters it
would thread through every read duplicates what `Closed` already expresses, for
little demonstrable benefit at this scope. The trade-off is explicit: a hard delete
is irreversible and leaves no audit trail (see below).

## What I'd improve with more time

Deliberate scope trade-offs, each recorded against the ADR that accepted it:

- **Soft delete + retention/audit.** Real lead data usually warrants a reversible
  delete with an audit trail rather than a permanent removal
  ([ADR-0006](docs/architecture/adr/0006-hard-delete.md)).
- **A durable CRM outbox.** Post-commit sync is best-effort: a permanently-down CRM
  or a crash silently drops the sync. Production needs an outbox/queue with retry, a
  dead-letter, and alerting on repeated failures
  ([ADR-0007](docs/architecture/adr/0007-crm-port-retry-logging.md),
  [C5](docs/architecture/contracts.md#c5-commit-boundary-cancellation-and-crm-delivery)).
- **Authentication and authorization.** Staff endpoints are open; a real deployment
  needs auth (and a CSRF strategy for any cookie-authenticated API), plus data
  retention and audit policy
  ([C7](docs/architecture/contracts.md#c7-web-ui-and-hosting)).
- **Concurrency and idempotency.** Status writes are last-committed-wins with no
  ETag/409; a repeated `POST` can duplicate intake. Optimistic concurrency and an
  idempotency key would harden both
  ([C4](docs/architecture/contracts.md#c4-listing-pagination-and-concurrent-triage),
  [C5](docs/architecture/contracts.md#c5-commit-boundary-cancellation-and-crm-delivery)).

## AI tools used

This project was developed with **Claude Code** (Anthropic) as a coding assistant,
under human direction and review. It was used to: draft the architecture decision
records, domain model, and boundary contracts under `docs/`; implement the backend,
frontend, and tests following a test-first (red → green → refactor) workflow; run
and report the test suites; and write this documentation. All design decisions,
scope trade-offs, and the final review were made and verified by the author; the
assistant's output was checked against the running tests and the assessment brief.

## More documentation

- [Architecture](docs/architecture/) — ADRs, boundary contracts, and interactive
  diagrams (`docs/architecture/index.html`).
- [Domain model](docs/domain/course-inquiry.md) — the inquiry entity, lifecycle, and
  business rules.
- [Backend](docs/backend/README.md) — API surface, service rules, persistence, CRM.
- [Frontend](docs/frontend/README.md) — the island's build, state model, and UX rules.
- [Written answers](written-answers.md) — troubleshooting, security, accessibility,
  code quality.
- [Testing](docs/testing/tdd-plan.md) — the TDD plan, case catalogs, and evidence.
- [Changelog](docs/CHANGELOG.md).
