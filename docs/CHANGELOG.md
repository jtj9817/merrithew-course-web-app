# Changelog

All notable changes to this project are documented here, following
[Keep a Changelog](https://keepachangelog.com). The project is not versioned or
tagged (it's a single assessment deliverable), so changes are grouped under
`[Unreleased]` with dated notes.

## [Unreleased]

### Added — troubleshooting demonstration (September 17, 2026)

- **Reproducible "missing inquiry" demonstration**: the written Troubleshooting
  answer can now be demonstrated live in the dashboard, branch by branch, via the
  [missing-inquiry runbook](runbooks/missing-inquiry.md) walkthrough. Delivers
  what OBS-101 did not stage:
- **`missing-inquiries` scenario**: a 26-row dataset (worked backlog + recent
  `New` rows a non-`All` filter hides, one resubmitted duplicate email, and
  page-2 volume), seeded through the existing `/api/dev/scenarios` path.
- **Intake fault injection** (development-only): `/api/dev/intake-fault`
  (`Development`, or `DevTools:IntakeFault=true`) arms the next create(s) to fail
  **before any write**, so a real `POST /api/inquiries` returns a genuine `500`
  with a `traceId` and stores no row — the "backend failure → never stored" case.
  Inert until armed; only the dev endpoint can arm it. A dashboard **Intake
  fault** control drives the arm-and-submit demonstration.
- **Reconciliation reports** (development-only): read-only `/api/dev/reconciliation`
  (`Development`, or `DevTools:Reconciliation=true`) returns count-by-status
  (incl. zeros), a last-7-days total, and duplicate-email groups, plus
  `by-email?email=…` for the stored-but-hidden vs never-stored tiebreaker — all
  parameterized EF Core LINQ. Mirrored by the runnable SQLite report file
  `database/reconciliation.sqlite.sql` and a dashboard **Reconciliation** readout.
- **Automated coverage**: UT-FAULT-001..006 (fault switch semantics, thread
  safety), IT-FAULT-001..005 and IT-RECON-001..003 (endpoint contracts, armed
  `500` with no row stored + serverError metric/log/`traceId`, reconciliation
  correctness, gating outside Development), and frontend component tests for the
  intake-fault and reconciliation controls — see the
  [plan](plans/troubleshooting-demonstration-plan.md).

### Added — runtime CRM simulation controls (September 17, 2026)

- **Selectable simulated CRM outcome at runtime**: `CrmSimulation` configuration
  (validated at startup) and an opt-in `/api/dev/crm-simulation` endpoint group
  (`Development`, or `DevTools:CrmSimulation=true`) choose among `Success`,
  `TransientThenSuccess`, `AlwaysTransientFailure`, `PermanentFailure`,
  `Timeout`, and `InternalCancellation`. The simulated external boundary now
  receives an explicit CRM payload; per-attempt settings are snapshotted per
  sync. Safe, non-PII sync results (inquiry id, mode, outcome, attempts) are
  exposed for the dashboard demonstrator.
- **CRM-originated cancellation isolated**: an `OperationCanceledException`
  thrown by the CRM itself can no longer escape inquiry creation after the
  commit — the row and the `201` response are kept.
- **Dashboard CRM demonstrator** (development-only): a dev control beside the
  scenario switcher picks a behavior and runs one real inquiry through
  `POST /api/inquiries`, reporting the sync outcome and attempt count from safe
  metadata; demo inquiries appear in the normal queue.
- **Automated coverage**: UT-CRM-011..017 (runtime modes through the real
  pipeline, privacy) and IT-CRM-SIM-001..008 (endpoint contract, gating,
  cancellation isolation, result readback) plus island component tests — see
  the [plan](plans/crm-simulation-plan.md).

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
