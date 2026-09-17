# Changelog

All notable changes to this project are documented here, following
[Keep a Changelog](https://keepachangelog.com). The project is not versioned or
tagged (it's a single assessment deliverable), so changes are grouped under
`[Unreleased]` with dated notes.

## [Unreleased]

### Added — initial build (through September 17, 2026)

The full Course Inquiry Dashboard, delivered as one ASP.NET Core deployable over a
local SQLite file. What a user of the app or the API can now do:

- **Inquiry API** at `/api/inquiries`: create, list (with `status` filter and
  `page`/`pageSize`/`sort`), read one, update status, and hard-delete — with
  validated input, RFC 7807 `ProblemDetails` errors, and sanitized `500`s. Statuses
  are the five names New/Contacted/Pending/Registered/Closed; `CreatedDate` and
  `UpdatedDate` are server-assigned UTC. See [backend docs](backend/README.md).
- **Staff dashboard** at `/dashboard`: a React island to view, filter, sort, and
  page the queue, inspect an inquiry's details, and update its status with clear
  success/error feedback. Keyboard-operable, screen-reader labelled, and
  color-independent. See [frontend docs](frontend/README.md).
- **Swagger UI** at `/swagger` (Development) documenting all five endpoints and the
  status enum — the create/delete surface for manual use.
- **Simulated CRM sync** on create: awaited after the inquiry commits, with a Polly
  retry/timeout pipeline and privacy-safe structured logging (no visitor data).
- **`database/database.sql`**: a SQL Server companion script — schema, six synthetic
  samples, and the last-7-days / count-by-status / duplicate-email report queries.
- **Automated tests**: an xUnit suite (unit, SQLite integration, host integration,
  and a separate SQL Server lane) and a Vitest component/unit suite, developed
  test-first. Results are recorded in the
  [testing evidence](testing/tdd-plan.md#phase-7-implementation-record).
- **Documentation**: README, written answers, ADRs, boundary contracts, domain
  model, interactive architecture diagrams, and these backend/frontend docs.

### Accepted limitations (by design, at assessment scope)

Behavior a downstream reader should know before relying on this build:

- **`DELETE` is a hard delete** — irreversible, no audit trail; use the `Closed`
  status to retain-but-deactivate ([ADR-0006](architecture/adr/0006-hard-delete.md)).
- **No authentication/authorization** — staff endpoints are open; synthetic data and
  local/demo use only ([C7](architecture/contracts.md#c7-web-ui-and-hosting)).
- **CRM sync is best-effort** — no durable outbox, so a crash or a permanently-down
  CRM can silently drop a sync; the stored inquiry is unaffected
  ([ADR-0007](architecture/adr/0007-crm-port-retry-logging.md)).
- **Status writes are last-committed-wins** (no ETag/409) and intake is not
  idempotent (a repeated `POST` can create a duplicate).
