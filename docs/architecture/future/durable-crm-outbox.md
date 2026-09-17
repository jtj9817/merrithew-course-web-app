# Future design: Durable CRM outbox

> **Status: not implemented.** The current CRM integration is deliberately
> synchronous, bounded, and best-effort after the inquiry commits. Adopt an
> outbox only when CRM delivery is a business requirement rather than an
> assessment simulation.

## Why and when to adopt it

Today `InquiryService.CreateAsync` commits `CourseInquiry`, then awaits
`ICrmClient.SyncInquiryAsync`. CRM exceptions are isolated so the row remains
stored, but a process stop after commit can lose the sync permanently
([contracts C5–C6](../contracts.md#c5-commit-boundary-cancellation-and-crm-delivery),
[ADR-0007](../adr/0007-crm-port-retry-logging.md)). The current Polly retries are
bounded request-time resilience; they are not durable delivery.

Adopt this design when the business requires eventual CRM delivery across process
restarts, extended outages, or transient deployment failures, and is willing to
operate a delivery backlog and dead-letter process.

## Goals

- Commit the inquiry and a durable delivery intent atomically.
- Return from intake without waiting for CRM availability.
- Resume delivery after process restart.
- Retry transient failures with durable scheduling and dead-letter permanent or
  exhausted messages.
- Preserve the existing rule that CRM failure cannot roll back an inquiry.
- Keep visitor payloads and raw CRM errors out of application logs.
- Expose backlog health so lost or delayed delivery is visible operationally.

## Non-goals

- Do not claim exactly-once delivery; the boundary is at-least-once.
- Do not make the CRM the source of truth for inquiry creation.
- Do not reuse ordinary log entries as the queue.
- Do not add a message broker unless throughput or isolation requires one; the
  database outbox is the initial durable boundary.
- Do not retain outbox payloads indefinitely.

## Proposed components

| Component | Responsibility | Runs as | Talks to |
| --- | --- | --- | --- |
| Intake transaction | Writes the inquiry and one outbox message atomically | Existing request path | Application database |
| `CrmOutboxWorker` | Claims due messages, invokes CRM, records outcome | `BackgroundService` in the existing ASP.NET Core process | Outbox table, `ICrmClient` |
| `ICrmClient` | Executes one bounded external delivery attempt | Existing CRM port with a real provider implementation | External CRM |
| Outbox monitor | Reports pending age/count, retry rate, and dead letters | Existing operational surface | Outbox table/metrics sink |

Keeping the worker in the existing process preserves the single-deployable shape.
The table contract allows the worker to move into a separate process later without
changing intake semantics.

## Data ownership

Add a `CrmOutboxMessages` table written only by the application transaction and
worker:

| Field | Purpose |
| --- | --- |
| `MessageId` | Globally unique delivery identifier and downstream idempotency key |
| `InquiryId` | Source inquiry identifier |
| `EventType` | Versioned event name, initially `InquiryCreated` |
| `SchemaVersion` | Payload compatibility version |
| `Payload` | Immutable CRM payload protected according to the production data-at-rest policy |
| `OccurredAtUtc` | Time the inquiry and intent committed |
| `AvailableAtUtc` | Earliest next delivery attempt |
| `AttemptCount` | Number of completed delivery attempts |
| `Status` | `Pending`, `Processing`, `Delivered`, or `DeadLetter` |
| `LeaseUntilUtc` | Recovery boundary for an abandoned claim |
| `DeliveredAtUtc` | Successful completion time |
| `LastErrorType` | Safe allow-listed category only, never a raw message or exception |

The outbox needs an immutable payload rather than only `InquiryId`: the current API
allows hard deletion, so a worker could otherwise lose the data before delivery.
That duplicates visitor data and therefore requires explicit encryption/key
management, access control, retention, and purge rules. ASP.NET Core Data
Protection is suitable only with a persistent, shared key ring and an agreed key
lifetime; do not create ciphertext that a restarted or multi-instance worker
cannot decrypt.

## Intake flow

1. Validate `CreateInquiryDto` exactly as today.
2. Begin one database transaction.
3. Insert `CourseInquiry` with server-owned `New` status and UTC timestamps.
4. Insert one `CrmOutboxMessages` row containing the immutable, versioned CRM
   payload and a new `MessageId`.
5. Commit both writes atomically.
6. Return `201 Created`. Do not call the CRM in the request path.
7. Wake the worker as an optimization; correctness must depend on polling the
   durable table, not on an in-memory signal.

If either insert fails, neither row commits. A committed inquiry therefore always
has its corresponding CRM delivery intent. If idempotency keys are also adopted,
the inquiry, idempotency record, and outbox message belong in this same transaction
([idempotency design](idempotency-keys.md)).

## Worker flow

1. Poll for the oldest due `Pending` message.
2. Atomically claim it by setting `Processing` and a short lease. A multi-instance
   deployment must use a provider-appropriate lease/concurrency strategy; a plain
   read followed by update is not a safe claim.
3. Call `ICrmClient` once with the payload, `MessageId` as the downstream
   idempotency key, a per-attempt timeout, and the worker cancellation token.
4. On success, mark the message `Delivered` and set `DeliveredAtUtc`.
5. On a transient failure, increment `AttemptCount`, calculate the next durable
   `AvailableAtUtc`, clear the lease, and return it to `Pending`.
6. On permanent failure or exhausted attempts, mark `DeadLetter` and raise an
   operational alert.
7. On process termination, the lease expires and another worker can retry.

Use durable retry scheduling instead of nesting the current four-attempt request
pipeline inside every worker attempt. Retain bounded per-attempt timeout and safe
error classification, but configure one retry owner so attempts do not multiply
unexpectedly.

## Delivery semantics

The guarantee is **at least once**. A worker can receive CRM success and terminate
before marking the message `Delivered`; the message will run again after its lease
expires. The CRM endpoint must accept `MessageId` as an idempotency key or provide
an equivalent deduplication mechanism. Without downstream idempotency, duplicate
CRM records remain possible and must be an explicitly accepted risk.

A `201` response means the inquiry and delivery intent are durable, not that the
CRM has accepted the record. Do not add a synchronous success claim to
`InquiryResponse`. If staff need delivery visibility, expose a separate restricted
status/operations view rather than coupling intake to worker completion.

## Privacy and security constraints

- Outbox payloads contain visitor data; encrypt them at rest with recoverable,
  rotated keys and restrict table access.
- Logs and metrics use `MessageId`, `InquiryId`, attempt number, status, duration,
  and safe error category only.
- Never log payloads, CRM response bodies, raw exceptions, tokens, or endpoint
  credentials.
- Purge delivered payloads after an agreed short retention period; retain only
  minimal non-PII delivery metadata if operational history is required.
- Dead-letter inspection and replay are privileged operations and must themselves
  be audited once the access-control design is adopted.

## Failure behavior

| Failure | Required outcome |
| --- | --- |
| Inquiry insert fails | No inquiry and no outbox message |
| Outbox insert fails | Transaction rolls back; no inquiry is reported created |
| Process stops after commit, before worker runs | Pending message survives restart and is delivered later |
| CRM is unavailable | Message remains pending with durable backoff; intake remains available |
| Worker stops while leased | Lease expires; message becomes eligible again |
| CRM succeeds but delivery mark fails | Message may be sent again; downstream idempotency prevents duplicate effect |
| Payload cannot be decrypted/deserialized | Dead-letter with safe category and alert; never discard silently |
| Permanent rejection or retry exhaustion | Dead-letter and alert; inquiry remains stored |

## Operations required

- Metrics: pending count, oldest pending age, attempt rate, success rate, retry
  count, dead-letter count, and lease-recovery count.
- Alerts: oldest age above the business delivery objective, any sustained dead
  letters, or worker heartbeat absence.
- Restricted tooling: inspect safe metadata, replay after correction, and mark an
  irrecoverable message resolved with an audit reason.
- Capacity controls: bounded batches, cancellation-aware shutdown, retention
  cleanup, and indexes on `(Status, AvailableAtUtc)` and lease expiry.

## Rollout sequence

1. Agree the delivery objective, maximum attempts, backoff, dead-letter owner,
   payload retention, encryption/key management, and downstream idempotency
   contract.
2. Add the outbox migration and payload schema version.
3. Change creation to write inquiry plus outbox message in one transaction and
   remove synchronous CRM delivery from the request path.
4. Deploy the worker initially disabled, verify that new creates produce valid
   pending records, then enable processing.
5. Add monitoring, alerting, dead-letter inspection, and controlled replay.
6. Remove obsolete request-time multi-retry behavior so the durable worker is the
   single retry owner.
7. Decide explicitly whether existing inquiries require backfill; do not enqueue
   historical rows silently.

## Verification required before adoption

- Inquiry and outbox writes are atomic under injected failures and cancellation.
- A committed pending message survives process termination and restart.
- Worker claim/lease logic prevents concurrent processing except the accepted
  crash-after-remote-success duplicate window.
- Transient failures persist increasing attempts and future availability; permanent
  failures and exhaustion dead-letter correctly.
- Downstream idempotency prevents duplicate effects after an ambiguous success.
- Hard deletion before delivery does not remove the outbox payload.
- Delivered payload retention cleanup does not remove pending/dead-letter work.
- Configured sinks contain no visitor values, payloads, credentials, response
  bodies, or raw exceptions.

Adopting this design changes ADR-0007’s accepted delivery policy and requires a new
superseding ADR. Until then, the current bounded best-effort behavior remains the
implemented contract.
