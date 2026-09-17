# Changelog

All notable changes to this project are documented here, following
[Keep a Changelog](https://keepachangelog.com). The project is not versioned or
tagged (it's a single assessment deliverable), so changes are grouped under
`[Unreleased]` with dated notes.

## [Unreleased]

### Added — runtime CRM simulation controls (September 17, 2026)

- **Selectable simulated CRM outcome at runtime**: `CrmSimulation` configuration
  (validated at startup) and an opt-in `/api/dev/crm-simulation` endpoint group
  (`Development`, or `DevTools:CrmSimulation=true`) choose among `Success`,
  `TransientThenSuccess`, `AlwaysTransientFailure`, `PermanentFailure`,
  `Timeout`, and `InternalCancellation`. The simulated external boundary now
  receives an explicit CRM payload; per-attempt settings are snapshotted per
  sync. Safe, non-PII sync results (inquiry id, mode, outcome, attempts) are
  exposed for the upcoming dashboard demonstrator.
- **CRM-originated cancellation isolated**: an `OperationCanceledException`
  thrown by the CRM itself can no longer escape inquiry creation after the
  commit — the row and the `201` response are kept. The dashboard island
  control for these modes is planned under
  [Phase 10](../TODO.md) (see the [plan](plans/crm-simulation-plan.md)).

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
- **Future production designs**: conditional implementation plans for
  authentication/authorization with audit history, a durable CRM outbox, and
  client-supplied idempotency keys under `architecture/future/`.

### Accepted limitations (by design, at assessment scope)

Behavior a downstream reader should know before relying on this build:

- **`DELETE` is a hard delete** — irreversible, no audit trail; use the `Closed`
  status to retain-but-deactivate ([ADR-0006](architecture/adr/0006-hard-delete.md);
  [future auth/audit design](architecture/future/authentication-authorization-audit.md)).
- **No authentication/authorization** — staff endpoints are open; synthetic data and
  local/demo use only ([C7](architecture/contracts.md#c7-web-ui-and-hosting);
  [future design](architecture/future/authentication-authorization-audit.md)).
- **CRM sync is best-effort** — no durable outbox, so a crash or a permanently-down
  CRM can silently drop a sync; the stored inquiry is unaffected
  ([future outbox design](architecture/future/durable-crm-outbox.md)).
- **Status writes are last-committed-wins** (no ETag/409) and intake is not
  idempotent; automated retry clients would need the
  [idempotency-key design](architecture/future/idempotency-keys.md).
