# Merrithew Course Inquiry Dashboard

Technical assessment: ASP.NET Core Web API for course registration inquiries,
with a planned Razor/React dashboard and simulated CRM integration.

## Implemented scope

Phases 0–2 of [TODO.md](TODO.md) are implemented: .NET 10 solution and xUnit
harness, EF Core/SQLite entity mapping and startup migrations, SQL Server
companion script, and validated request/response DTOs. **The inquiry service,
CRM implementation, five inquiry endpoints, and dashboard are not implemented
yet.** Swagger currently serves an empty API document. Test-only controllers
exercise DTO binding through the actual MVC host without shipping fake endpoints.

## Setup and run

Requires the **.NET 10 SDK**. No external database is needed for the application.

```bash
dotnet restore
dotnet tool restore
dotnet build
dotnet test --filter 'Category!=SqlServer'
dotnet run --project backend
```

The HTTP launch profile listens at `http://localhost:5083`; Swagger UI is at
`http://localhost:5083/swagger`. It is enabled only in Development. Startup
applies EF migrations before listening, creating `backend/inquiries.db` by
default. A migration failure stops startup; there is no in-memory fallback.
Override the connection using `ConnectionStrings__DefaultConnection`.

```bash
dotnet ef migrations add <MigrationName> --project backend
dotnet ef database update --project backend
```

Only synthetic data and local demo use are appropriate. Authentication and
production deployment hardening are outside the assessment scope.

## Test lanes

```bash
dotnet test --filter 'Category=Unit'
dotnet test --filter 'Category=Integration'
dotnet test --filter 'Category=SqlServer'
```

The SQL Server lane requires `SQLSERVER_TEST_CONNECTION_STRING` pointing at an
**explicitly disposable SQL Server**, with `Initial Catalog=CourseInquiryTests_bootstrap`
(or another `CourseInquiryTests_`-prefixed name). Supply credentials securely in
the environment, not in source control. That catalog need not exist: the harness
uses `master` solely to create/drop a unique owned test database for each case.
The account needs database creation/deletion privileges. No supplied database is
opened or reset. Missing infrastructure fails with a `Blocked` diagnostic rather
than silently skipping tests. Unfiltered `dotnet test` includes this lane.

`database/database.sql` is SQL Server T-SQL, **not** SQLite bootstrap SQL. It
contains matching DDL, six synthetic rows, and the three required reports. The
application instead uses EF migrations: SQLite uses `INTEGER` autoincrement and
text timestamps, while SQL Server uses `IDENTITY` and `datetime2(7)`. SQLite does
not enforce EF length metadata; DTO validation enforces the UTF-16 limits. UTC
kind is restored when EF reads timestamps. Repeated emails are allowed.

## Structure

```text
backend/     Controller-based ASP.NET Core host, Models/, Models/Dtos/, Migrations/
database/    SQL Server schema, synthetic samples, and report queries
tests/       xUnit Unit/, Integration/, SqlServer/, and isolated Fixtures/
frontend/    Reserved for the Phase 6 React island
docs/        Architecture, domain contracts, and test catalogs
```

## Implementation record

- Phases 0–2: 91 local unit/HTTP/SQLite tests and 8 SQL Server tests passed.
- Live application smoke: startup migration, Swagger HTTP 200, process restart,
  and retained synthetic row verified.
- [Detailed evidence and scope](docs/testing/tdd-plan.md#phases-02-implementation-record).

The architecture verification model remains planned: its aggregate identifiers
also require service, endpoint, CRM, and UI cases from later phases. Historical
ADRs are unchanged. This implementation was developed with AI coding assistance.
