# Future design: Idempotency keys for inquiry intake

> **Status: not implemented.** The current `POST /api/inquiries` deliberately
> allows identical submissions and repeated email addresses. Adopt idempotency
> keys only when an intake client automatically retries requests or transport
> failures create an unacceptable duplicate-inquiry risk.

## Why and when to adopt it

A caller can lose the response after the database commit and retry the same POST.
The current contract creates another row because it cannot distinguish a retry
from a visitor intentionally making a second inquiry
([contract C1](../contracts.md#c1-intake-validation-and-representation),
[contract C5](../contracts.md#c5-commit-boundary-cancellation-and-crm-delivery)).
Email or body-based deduplication would be incorrect: one visitor may legitimately
ask about multiple courses or submit the same request again.

Adopt this design when a known client retries automatically and can preserve an
opaque key across every attempt for one logical submission.

## Goals

- Make retried `POST /api/inquiries` requests converge on one inquiry when they
  carry the same key and equivalent validated payload.
- Preserve intentional repeat inquiries by requiring a new key for each new user
  intent.
- Resolve concurrent retries with a database uniqueness constraint, not an
  in-memory cache.
- Return a clear conflict when a key is reused for different content.
- Avoid storing or logging raw keys, request bodies, or duplicate response
  snapshots.
- Compose atomically with a future CRM outbox.

## Non-goals

- Do not infer duplicates from email, names, course, time windows, or full-body
  equality.
- Do not promise idempotency when the client omits the key.
- Do not make inquiry status updates idempotent through this mechanism; their
  existing same-status no-op contract is separate.
- Do not claim exactly-once network delivery or CRM processing.
- Do not retain keys forever.

## HTTP contract

Use the `Idempotency-Key` request header on `POST /api/inquiries`:

- The key is an opaque, high-entropy client-generated value. A UUID is suitable;
  values must have a documented bounded length and allowed character set.
- One key represents one logical form submission. Network retries reuse it; a
  deliberate second inquiry uses a new key.
- During migration the header may remain optional for compatibility, but any
  automatically retrying client **must** send it. If it later becomes mandatory,
  version that contract explicitly rather than silently changing existing clients.
- The server stores a cryptographic hash of the normalized key, never the raw
  value. Do not write the key to access/application logs.
- Scope uniqueness to the operation and, when a trusted client identity exists,
  that client identity. Without authenticated clients, use the endpoint scope plus
  a sufficiently random global key.

Responses:

| Situation | Response |
| --- | --- |
| First valid request commits | `201 Created` with the normal `Location` and `InquiryResponse` |
| Same key and same request fingerprint, inquiry still exists | Replay the original success semantics: `201`, same `Location`, current persisted inquiry representation, plus `Idempotency-Replayed: true` |
| Same key but different fingerprint | `409 ProblemDetails`; no write and no CRM action |
| Same key after its inquiry was hard-deleted but before key expiry | `410 ProblemDetails`; do not recreate the inquiry |
| Missing optional key | Existing behavior: each successful request may create a new row |
| Expired key | Treated as new; clients must not retry beyond the advertised retention window |

Returning `410` after hard deletion preserves the idempotency guarantee without
retaining a second PII-bearing response snapshot. It also makes the irreversible
delete visible rather than silently creating a replacement row.

## Data model

Add an `InquiryIdempotencyRecords` table:

| Field | Purpose |
| --- | --- |
| `Scope` | Stable operation/client namespace |
| `KeyHash` | Cryptographic hash of the normalized key |
| `RequestHash` | Versioned fingerprint of the validated create fields |
| `InquiryId` | Logical result identifier; retained if the inquiry is hard-deleted |
| `CreatedAtUtc` | Commit time |
| `ExpiresAtUtc` | End of the supported replay window |

Use a unique constraint on `(Scope, KeyHash)`. Do not use a cascading foreign key
to `CourseInquiries`; the record must survive hard deletion until expiry so a
retry cannot recreate the deleted inquiry. The record contains no visitor fields
or response body.

The request fingerprint is a cryptographic hash of a canonical, versioned
representation built from the seven validated `CreateInquiryDto` fields in fixed
order. JSON property order and unknown/server-owned input fields must not affect
it. Preserve existing semantics: optional `null`, empty string, and whitespace
remain distinct where the current DTO contract preserves them. Include a schema
version so future field changes cannot accidentally compare incompatible hashes.

## Atomic create flow

1. Validate and bind the request before reserving a key. Invalid requests return
   the existing `400` and create no idempotency record.
2. Normalize and hash the key; compute the versioned request fingerprint.
3. Begin a database transaction.
4. Insert the inquiry and its idempotency record in the same transaction.
5. If both inserts succeed, commit and continue with the current post-commit CRM
   behavior.
6. If `(Scope, KeyHash)` conflicts, roll back the attempted inquiry, load the
   existing idempotency record, and compare `RequestHash`.
7. For the same fingerprint, return the existing inquiry without invoking CRM
   again. For a different fingerprint, return `409`. If the referenced inquiry
   was hard-deleted, return `410`.

The unique database constraint is the concurrency arbiter. A check-then-insert in
application memory is racy. Catch only the specific unique-key violation; unrelated
database failures must continue to surface through the sanitized `500` path.

If the durable CRM outbox is adopted, write the inquiry, idempotency record, and
single outbox message in this same transaction. A replay returns the existing
inquiry and never creates a second outbox message
([outbox design](durable-crm-outbox.md)).

## Cancellation and failure behavior

| Failure | Required outcome |
| --- | --- |
| Validation fails | No inquiry or idempotency record |
| Definite transaction failure | Neither row commits; retry with the same key may create |
| Response is lost after commit | Retry with the same key returns the committed inquiry |
| Two identical requests race | One inquiry commits; the loser replays it |
| Same key races with different payloads | One commits; the other receives `409` |
| CRM fails after commit | Inquiry/key remain committed; same-key retry does not invoke CRM again |
| Inquiry is deleted before retry | `410` until key expiry; no recreation |
| Cleanup removes an expired key | A later request may create a new inquiry, per the published retention window |

## Security and abuse controls

- Apply a strict header length/character limit before hashing to prevent allocation
  and storage abuse.
- Use a modern cryptographic hash and fixed-time comparison where raw digest
  comparison is exposed to application code.
- Never log the raw key, request fingerprint input, visitor payload, or replayed
  response.
- Rate-limit anonymous intake independently; idempotency keys are not an abuse or
  authentication control.
- Bound table growth with an indexed expiry cleanup job and monitor cleanup lag.
- Do not allow a caller to query an idempotency record directly by key.

## Client responsibilities

- Generate the key once when the visitor commits the form, not on every HTTP
  attempt.
- Persist it until a terminal response or the server’s advertised retention window
  expires.
- Retry only transport failures and explicitly retryable server responses using
  the same body and key.
- Treat `409` as a client bug/key-reuse error and `410` as a completed-but-deleted
  submission, not as permission to retry with the same key.
- Generate a new key when the visitor intentionally submits another inquiry.

## Rollout sequence

1. Agree which clients retry, key format/length, optional-versus-required rollout,
   retention window, cleanup ownership, and client namespace.
2. Add the idempotency migration and unique constraint.
3. Add deterministic request fingerprinting and transactional create behavior.
4. Update trusted clients to generate and persist one key per logical submission.
5. Publish the replay, conflict, deletion, and expiry semantics in OpenAPI/API docs.
6. Monitor replay/conflict/expiry rates and idempotency-table growth.
7. Require the header only after every automatically retrying client has migrated.

Existing inquiry rows are not backfilled because no historical key exists. The
feature begins with requests received after deployment.

## Verification required before adoption

- Sequential and concurrent same-key/same-body requests create exactly one inquiry
  and return the same identity.
- Same key with a changed field returns `409` without persistence or CRM work.
- JSON property order and ignored server-owned fields do not change the fingerprint;
  meaningful distinctions such as optional `null` versus empty remain distinct.
- Database failure rolls back both inquiry and idempotency record.
- Lost-response simulation followed by retry returns the committed inquiry.
- CRM failure does not remove the key, and replay does not repeat CRM work.
- Hard delete followed by replay returns `410`; expiry cleanup later allows new
  processing according to the documented window.
- Logs and error responses contain no raw key, fingerprint input, or visitor values.

Adopting this design changes the current duplicate-submission contract and requires
a new ADR plus updates to C1, C3, C5, OpenAPI, client behavior, and the integration
case catalogs.
