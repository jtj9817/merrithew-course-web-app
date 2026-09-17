# Future design: Authentication, authorization, and audit history

> **Status: not implemented.** The assessment intentionally ships an open,
> synthetic-data-only API. Adopt this design before the dashboard or staff API
> handles real visitor data outside a controlled local/demo environment.

## Why and when to adopt it

The current process registers controllers and Razor Pages but no authentication or
authorization middleware (`backend/Program.cs`). Every inquiry route is therefore
public, including reads, status changes, and permanent deletion. Same-origin
requests and a page labelled “staff” are not security boundaries
([contract C7](../contracts.md#c7-web-ui-and-hosting)).

Adopt this design when any of the following becomes true:

- real visitor data is stored;
- the service is reachable outside a trusted local environment;
- staff actions must be attributable to a person;
- deletion or status-change history must survive the current row; or
- policy or regulation requires access and retention evidence.

Authentication, authorization, and audit history belong in one change because an
audit event without a trustworthy actor identity is weak evidence.

## Goals

- Keep visitor intake possible without a staff account.
- Require an authenticated staff identity for the dashboard and staff operations.
- Apply least-privilege policies to read, update, and delete operations.
- Record create, status-change, and delete events durably without copying visitor
  names, contact details, or messages into the audit store.
- Commit each staff mutation and its audit event atomically.
- Preserve audit events after a hard delete while enforcing a documented retention
  policy.

## Non-goals

- Do not build a local password database or custom identity provider.
- Do not treat application logs as the audit history.
- Do not record access tokens, cookies, identity claims, request bodies, or visitor
  fields in audit events or logs.
- Do not add visitor accounts; the public form remains an external intake client.
- Do not promise tamper-proof regulatory evidence until storage permissions,
  retention, export, and independent monitoring are defined operationally.

## Proposed components

| Component | Responsibility | Runs as | Talks to |
| --- | --- | --- | --- |
| External OIDC provider | Authenticates staff and issues identity/role claims | External service | ASP.NET Core host |
| ASP.NET Core authentication | Authorization-code flow, secure session cookie, sign-in/out | Existing web process | OIDC provider |
| Authorization policies | Maps trusted claims to inquiry read/manage/delete permissions | Existing web process | Controllers and Razor Pages |
| Audit writer | Creates minimal append-only events in the mutation transaction | Application layer | `AppDbContext` |
| Audit store | Retains mutation history independently of the inquiry row | Existing application database | Audit writer and restricted reporting path |

The identity provider and exact claim mapping are deployment decisions. Prefer a
managed organizational provider over application-owned credentials. Validate
issuer, audience, signature, lifetime, and the specific claim used for policy
mapping; never infer authorization from an email domain alone.

## Authorization surface

Use named policies rather than role checks scattered through controller actions:

| Surface | Policy |
| --- | --- |
| `POST /api/inquiries` | Anonymous intake; separately rate-limited and abuse-protected |
| `GET /api/inquiries` | `InquiryRead` |
| `GET /api/inquiries/{id}` | `InquiryRead` |
| `PUT /api/inquiries/{id}/status` | `InquiryManage` |
| `DELETE /api/inquiries/{id}` | `InquiryDelete` |
| `/dashboard` | `InquiryRead` |
| `/swagger` | Development only; authenticated if enabled in any shared environment |

An initial deployment may map all three policies to one staff group while keeping
the policies separate. That preserves a clean path to restrict permanent deletion
without changing endpoint code.

## Staff request flow

1. A staff member requests `/dashboard` and is challenged through the selected
   OIDC provider when no valid session exists.
2. ASP.NET Core validates the returned identity and issues a short-lived,
   `Secure`, `HttpOnly` session cookie with an appropriate `SameSite` policy.
3. The Razor page and API enforce named policies. Unauthenticated requests receive
   `401` or an interactive challenge as appropriate; authenticated but unauthorized
   API requests receive `403`.
4. Browser mutations carry an antiforgery token because cookie credentials are
   attached automatically. The public visitor `POST` is not a staff-cookie action;
   it needs rate limiting/bot controls rather than a staff antiforgery requirement.
5. For a successful staff mutation, the application writes the domain change and
   its audit event in one database transaction.

## Audit data model

Add an append-only `InquiryAuditEvents` table owned by the application layer:

| Field | Purpose |
| --- | --- |
| `Id` | Monotonic event identifier |
| `InquiryId` | Logical inquiry identifier; retained after hard delete |
| `EventType` | Closed set: `Created`, `StatusChanged`, `Deleted` |
| `ActorSubject` | Stable opaque provider subject, or `anonymous` for public intake |
| `FromStatus` / `ToStatus` | Status transition only; null for unrelated events |
| `OccurredAtUtc` | Injected `TimeProvider` instant |
| `CorrelationId` | Request correlation value for support investigation |

Do not store a cascading foreign key from the audit event to `CourseInquiries`:
a hard delete must not erase its own audit evidence. Do not copy names, email,
phone, location, course text, or message content into the audit row. If a future
compliance requirement needs a before-image, define its encryption and retention
separately rather than serializing the entity opportunistically.

Create, status update, and delete must each use an explicit database transaction
that contains both the business mutation and audit insert. If the audit write
fails, the mutation fails and rolls back; silently accepting an unaudited change
would defeat the feature. Reads are not audited by default because that produces a
high-volume access log with different retention and privacy requirements. Add read
access auditing only if a concrete compliance requirement demands it.

## Security constraints

- Persist only the opaque OIDC subject needed for attribution; keep display names
  outside durable events unless policy requires them.
- Never log tokens, cookies, claim collections, antiforgery values, or raw audit
  payloads.
- Rotate signing/session keys and share the data-protection key ring if the app
  becomes multi-instance.
- Set explicit session lifetime, idle timeout, reauthentication, and account
  revocation behavior with the identity owner.
- Restrict audit queries and exports more tightly than ordinary inquiry reads.
- Define retention and deletion rules for audit events before storing real data.
- Keep the current sanitized `ProblemDetails` behavior; authentication failures
  must not disclose account existence or provider details.

## Failure behavior

| Failure | Required outcome |
| --- | --- |
| Identity provider unavailable during sign-in | No session created; existing session behavior follows configured validation/lifetime rules |
| Authenticated user lacks a policy | `403`; no domain or audit write |
| Antiforgery validation fails | Safe `400`/`403`; no mutation |
| Audit insert fails | Domain mutation rolls back |
| Inquiry disappears during update/delete | Existing `404` contract; no false success audit event |
| Audit reporting is unavailable | Inquiry mutations continue only if the primary audit write remains durable; reporting availability is separate |

## Rollout sequence

1. Select the OIDC provider, trusted claims, policy mappings, session lifetime,
   audit retention, and the owner of access reviews.
2. Add authentication/authorization configuration with fail-closed production
   startup when required settings are absent.
3. Add the audit migration and atomic audit writer.
4. Protect staff Razor/API surfaces; keep only visitor intake anonymous.
5. Add antiforgery handling to same-origin staff mutations and update the React
   client to send the token.
6. Add restricted audit reporting and operational alerts for repeated `401`/`403`
   or audit-write failures.
7. Only then allow real visitor data or external network exposure.

## Verification required before adoption

- Anonymous access succeeds only for visitor intake and is rejected for every
  staff read/mutation surface.
- Each policy allows and denies the expected claim sets; missing/forged/expired
  tokens fail closed.
- Cookie-authenticated mutations fail without a valid antiforgery token.
- Status and delete audit events contain the correct actor, transition, time, and
  correlation ID without visitor data.
- Mutation and audit insert either both commit or both roll back under injected
  database failures and concurrent deletion.
- A hard-deleted inquiry leaves its minimal audit history intact.
- Configured logs and errors contain no tokens, cookies, claims, visitor payloads,
  or raw provider exceptions.

## Decisions still required

- OIDC provider and deployment-specific claim mapping.
- Whether one staff group initially receives every policy.
- Audit retention, export, and legal deletion requirements.
- Whether read-access auditing is required.
- Whether hard delete remains acceptable once audit and retention policy exist.

This design would supersede the open-endpoint limitation in
[contract C7](../contracts.md#c7-web-ui-and-hosting) and would require a new ADR;
it does not retroactively change the accepted assessment architecture.
