# 7. CRM integration via an ICrmClient port, with retry and redacted logging

Date: 2026-09-16
Status: Accepted

## Context

Part 4 requires a **simulated** CRM integration: accept inquiry data, log or store
that a sync was attempted, handle success and failure, and **avoid exposing
sensitive data in logs**. The bonus asks for retry logic / structured error
handling. There is no real CRM, endpoint, or API key. The overriding rule is that
the inquiry — not the CRM — is the record of truth (see REQ-SYS-003).

## Decision

- An **`ICrmClient` port** with a **`SimulatedCrmClient`** implementation.
- The `InquiryService` calls `SyncInquiryAsync` **after** the inquiry is
  persisted. A sync failure is caught and handled; it never rolls back or fails
  the create.
- Retry with **exponential backoff using Polly** for transient failures.
- Every attempt is recorded via the built-in **`ILogger`** as a structured entry
  (inquiry id + outcome), with **email and phone redacted**.

## Options considered

### Port + simulated client (chosen)
- The service depends on an abstraction, not a vendor: swappable later, and tests
  can force success or failure.

### Fake call inline in the service (rejected)
- Couples business logic to the fake; not substitutable; hard to test failure
  paths.

### Retry: Polly (chosen) vs hand-rolled (rejected)
- Polly is declarative and battle-tested; hand-rolled retry/backoff is easy to get
  subtly wrong.

### Recording the attempt: structured logs (chosen) vs Serilog (rejected) vs a `CrmSyncLog` table (rejected)
- Built-in `ILogger` needs no extra package; redaction is a code concern, not a
  library one. Serilog adds packages for an equivalent result at this scope. A DB
  table adds schema the spec does not require ("log **or** store").

## Consequences

**Good:** the inquiry is durable regardless of CRM health; transient failures are
retried; sensitive data never reaches logs; failure paths are unit-testable via
the port.

**Bad:** sync is **best-effort and fire-after-persist** — a permanently-down CRM
silently drops the sync (it is only logged). There is no durable outbox or
dead-letter.

**Watch for:** if CRM delivery becomes business-critical, supersede this
assessment decision with the [durable outbox design](../future/durable-crm-outbox.md):
transactional intent, durable retries, dead-letter handling, and alerting.

## Addendum — runtime outcome selection (2026-09-17)

The simulation's outcome is now selectable at runtime, not only via test
injection: a `CrmSimulationRuntime` executes one of six deterministic modes
(`Success`, `TransientThenSuccess`, `AlwaysTransientFailure`, `PermanentFailure`,
`Timeout`, `InternalCancellation`) inside the same Polly pipeline, configured
through the `CrmSimulation` section or the development-only
`/api/dev/crm-simulation` endpoints, and surfaced in the dashboard by a
development-only control ([plan](../plans/crm-simulation-plan.md)). This changes
no decision above: the mode runs inside the identical retry/timeout/logging
envelope, settings snapshot per sync, the simulated boundary receives an explicit
payload that no logging code accepts, results expose only inquiry id / mode /
outcome / attempts, and `InternalCancellation` is isolated post-commit exactly
like every other CRM failure. Delivery remains best-effort with no durable
outbox; the [durable outbox design](../future/durable-crm-outbox.md) is still
the supersession path.
