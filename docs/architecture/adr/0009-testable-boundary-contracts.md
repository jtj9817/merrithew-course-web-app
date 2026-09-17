# 9. Make boundary contracts explicit and develop behavior test-first

Date: 2026-09-16
Status: Accepted — implemented and verified through Phase 9

## Context

The documentation review requested a TDD flow for all planned behavior, with
unit and integration test cases. The repository currently contains planning
documents and directory placeholders, not a runnable application or test suite.
The assessment requires at least one automated test; this extension deliberately
raises that floor to all committed behaviors, without inventing production
features such as authentication, an outbox, or optimistic concurrency.

The earlier design leaves observable choices open: status omission/numeric
values, pagination shape, equal timestamps, duplicate submissions, cancellation
around a commit, retry exhaustion, and SQL-dialect verification. It also calls
CRM work “fire-after-persist,” which could be mistaken for an unobserved task.
A test cannot settle these questions by accidentally enshrining a framework
default.

## Decision

Adopt [boundary contracts C1–C8](../contracts.md) and the
[TDD extension](../../testing/tdd-plan.md) as the executable-test specification.
These are project choices made during this review, not newly discovered
assessment mandates. The existing topology and ADRs 0002–0008 remain in force,
with the following explicit refinements taking precedence over ambiguous prose:

- ADR-0008's enum binding requires presence, name-only conversion, and defined
  membership checks; binding alone is insufficient. Same-status PUT is a no-op.
- System timestamps are UTC observations, not strictly increasing versions.
- ADR-0007 means **awaited, bounded, post-commit best-effort sync**. It is not
  fire-and-forget and cannot guarantee delivery across a crash. The port signals
  failure through exceptions; the service isolates them only after commit.
- Omitting visitor fields is the selected redaction policy. Raw exception and
  telemetry paths must not bypass it.
- ADR-0004's shell supplies layout and guidance, not a usable queue before React
  boots. The island has no create/delete UI; those operations use API/Swagger.
- ADR-0003's two dialects require two kinds of evidence: SQLite integration tests
  for the application and SQL Server execution for the companion script.

Use xUnit for .NET unit/integration tests and Vitest with React Testing Library
for frontend unit/component integration tests. An EF-backed service test is an
**integration** test even when HTTP is absent. Keep ADR-0005's direct EF access;
do not add a repository or mock LINQ to obtain a “unit test” label. Retain a
real-browser acceptance walkthrough in addition to automation.

Every vertical slice starts with a meaningful failing case, then minimal working
behavior, then refactoring with the relevant tests green. A missing tool or
unavailable SQL Server is blocked evidence, not a passing verification.

## Options considered

### Explicit contracts plus focused unit/integration cases (chosen)

Makes outcomes reviewable before implementation and catches framework/provider
edge cases at the boundary that can actually reproduce them. Existing REQ/VER
identifiers remain stable; case IDs refine their procedures.

### Implement first, add optional tests at the end (rejected)

The old Phase 7 permits only one business-rule test and treats the rest as
optional. That is assessment-compliant but contradicts the requested TDD flow
and leaves failure isolation, UI races, and SQL semantics without evidence.

### Unit-test everything with mocked infrastructure (rejected)

Mocked DbSet queries, direct controller calls, and simulated SQL execution cannot
prove routing, JSON binding, relational constraints, migrations, or T-SQL syntax.
A repository solely for tests would also reverse ADR-0005 without a domain need.

### Production delivery/concurrency infrastructure now (rejected)

An outbox, idempotency keys, auth, and ETags solve real future problems, but they
change the agreed assessment scope. The conditional
[auth/audit](../future/authentication-authorization-audit.md),
[outbox](../future/durable-crm-outbox.md), and
[idempotency](../future/idempotency-keys.md) designs preserve those options
without implying the guarantees exist today.

## Consequences

**Good:** no hidden default becomes a business rule; failure and race cases have
observable outcomes; each requirement has planned automated evidence.

**Bad:** frontend test dependencies and a separate SQL Server verification lane
add work. SQL Server is required for full script-test evidence, although neither
application runtime nor the fast SQLite suite depends on it. Last-writer-wins,
possible duplicate POSTs, and lost CRM delivery remain accepted limitations.

**Watch for:** contracts and case catalogs remain executable specifications.
Return affected model statuses to `planned` whenever implementation changes invalidate evidence.
Use a new ADR if the accepted delivery, deletion, or concurrency policy changes;
do not rewrite the historical decisions.
