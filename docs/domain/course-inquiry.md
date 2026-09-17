# Domain: Course Inquiry

The bounded context is a single internal tool: staff at Merrithew triage **course
registration inquiries** that visitors submit from the public website. Each
creation attempts a best-effort simulated CRM sync after the inquiry is stored.

> **Target specification — entity, persistence, and DTO validation implemented.**
> Assessment requirements cite `option-1-course-inquiry-dashboard.md` as
> `spec:<line>`; decisions filling its gaps are identified separately through
> ADRs. [ADR-0009](../architecture/adr/0009-testable-boundary-contracts.md) and
> [boundary contracts](../architecture/contracts.md) refine the initial plan with
> edge-case semantics. The [TDD extension](../testing/tdd-plan.md) defines the
> implementation flow and unit/integration test cases; it is not passing evidence.

## Vocabulary

| Term | Means here | Does *not* mean |
| --- | --- | --- |
| **Inquiry** / CourseInquiry | A visitor's expression of interest in a course, captured from the website form. It is a *lead*, not a commitment. | A registration or enrolment — that happens in the external system, not here. Also not a support ticket. |
| **Visitor** | The public, unauthenticated person who submits an inquiry. | A staff user. |
| **Staff** | An internal user who views, filters, and changes inquiries via the admin list. | The visitor. The assessment does not include authentication: staff endpoints will be open, so only synthetic data and a local demo are safe. `written-answers.md` must discuss access control (`spec:98`). |
| **Status** | The workflow state of an inquiry as staff process it. | The delivery state of a course. |
| **CRM sync** | A *simulated* push of an inquiry to an external CRM. No real CRM or API key is involved. | A live integration. (`spec:75`) |

## Entities

### CourseInquiry

The only entity in the system. Represents one inquiry submitted by one visitor
about one course. Fields, as enumerated by the spec (`spec:15`–`spec:25`):

| Field | Meaning | Notes |
| --- | --- | --- |
| `Id` | Unique identifier | Int identity / autoincrement ([ADR-0003](../architecture/adr/0003-efcore-sqlite-and-sql-script.md)). |
| `FirstName` | Visitor's given name | Required-field validation applies (`spec:44`); exact required set below. |
| `LastName` | Visitor's family name | " |
| `Email` | Visitor's email | **Format must be validated** (`spec:44`). Duplicate-email reporting (`spec:59`) does not make it a unique key. |
| `Phone` | Visitor's phone | Optional (see the required-field rule below). |
| `CourseName` | The course the visitor is asking about | Free text in the spec; not a foreign key to a course catalogue. |
| `PreferredLocation` | Where the visitor would want to take the course | " |
| `Message` | Free-text note from the visitor | Optional (see the required-field rule below). |
| `Status` | Workflow state (see Lifecycle) | Defaults to `New` (`spec:45`). |
| `CreatedDate` | When the inquiry was received | **Set automatically** by the system, not the client (`spec:46`). |
| `UpdatedDate` | When the inquiry was created or its status last actually changed | **Set automatically**, never accepted from the client (`spec:46`). A same-status update is not a change. |

Field **types**: `Id` is an int identity ([ADR-0003](../architecture/adr/0003-efcore-sqlite-and-sql-script.md));
`Status` is a defined enum value ([ADR-0008](../architecture/adr/0008-free-form-status-transitions.md));
`CreatedDate`/`UpdatedDate` are `DateTime` (UTC); the rest are strings. Required
fields and exact length limits are specified in [contract C1](../architecture/contracts.md#c1-intake-validation-and-representation).
The entity has no course catalogue, visitor-account, or CRM-delivery entity
relationship; registration itself occurs outside this bounded context.

## Lifecycle

The five suggested statuses (`spec:27`) are the selected closed vocabulary,
not steps in a required forward-only workflow:

```text
Create                     → New
Any stored status          → Any defined status (including itself)
Any stored inquiry         → DELETE → No row (not Closed)
```

- **`New`** — just received; the default for every created inquiry (`spec:45`).
- **`Contacted`** — staff have reached out to the visitor.
- **`Pending`** — awaiting something (visitor decision, payment, a course date).
- **`Registered`** — the visitor has enrolled (the successful outcome).
- **`Closed`** — no further action for now; the row remains visible in unfiltered
  lists, may be reopened, and still counts in reports. It is not a soft-delete flag.

**Transitions are free-form** ([ADR-0008](../architecture/adr/0008-free-form-status-transitions.md)):
any valid status may be set in any order, and staff can correct in either
direction. `PUT /api/inquiries/{id}/status` (`spec:36`) is the only status update
path. Missing/null, unknown, numeric, and composite status inputs are rejected;
enum binding alone does not guarantee this. A same-status PUT succeeds without
changing timestamps. See [contract C2](../architecture/contracts.md#c2-status-and-timestamps).

## Rules

Assessment-mandated rules cite the spec; refinements cite the boundary contracts.
> Entity/storage and DTO rules have Phase 0–2 evidence; service/CRM/API behavior
> remains planned. See the [execution record](../testing/tdd-plan.md#phases-02-implementation-record).

- **Default status is `New`.** A newly created inquiry always starts at `New`,
  regardless of client input. (`spec:45`)
- **`CreatedDate` and `UpdatedDate` are system-assigned.** Create uses one UTC
  instant for both. An actual status change samples UTC again; a same-status
  update leaves them unchanged. Wall-clock time is not a strictly increasing
  version. (`spec:46`; [C2](../architecture/contracts.md#c2-status-and-timestamps))
- **Email must be a valid format.** Rejected with an appropriate error otherwise.
  (`spec:44`, `spec:47`)
- **Required fields must be present.** `FirstName`, `LastName`, `Email`, and
  `CourseName` are required; `Phone`, `PreferredLocation`, and `Message` are
  optional. Enforced with DataAnnotations on the request DTO
  ([ADR-0005](../architecture/adr/0005-layered-service-dto.md)). Required values
  cannot be blank; limits and optional-value preservation are in
  [C1](../architecture/contracts.md#c1-intake-validation-and-representation). (`spec:44`)
- **Invalid input and missing records return appropriate errors.** e.g. 400 for
  validation failure, 404 for an unknown `Id`. (`spec:47`)
- **Status is only changed through the dedicated endpoint.** Not via a general
  update. (`spec:36`)
- **Delete is a hard delete.** The row is removed permanently
  ([ADR-0006](../architecture/adr/0006-hard-delete.md)); the `Closed` status
  covers the "keep but deactivate" case. (`spec:37`, `spec:39`)
- **Repeat inquiries are allowed.** A visitor may ask about multiple courses or
  submit the same request again. There is no uniqueness/idempotency guarantee;
  the duplicate-email SQL query is reporting only. (`spec:59`;
  [C8](../architecture/contracts.md#c8-persistence-migration-and-sql-deliverable))
- **Persistence precedes best-effort CRM sync.** A definite failed write makes
  no CRM attempt. A CRM failure after commit cannot erase the row. Disconnection
  can hide a successful commit from the caller, and a crash can lose the sync;
  neither delivery nor exactly-once creation is promised. ([ADR-0007](../architecture/adr/0007-crm-port-retry-logging.md);
  [C5](../architecture/contracts.md#c5-commit-boundary-cancellation-and-crm-delivery))
- **CRM sync must not log sensitive data.** Log attempts/outcomes using inquiry
  ID and safe metadata, not visitor fields or raw exceptions. This covers
  structured fields and scopes as well as formatted messages. (`spec:78`–`spec:80`;
  [C6](../architecture/contracts.md#c6-crm-retry-timeout-and-privacy))
- **Concurrent changes are not a workflow history.** Last committed status
  write wins. A concurrent delete must not resurrect the row; no audit history
  or optimistic conflict response is promised.
  ([C4](../architecture/contracts.md#c4-listing-pagination-and-concurrent-triage))

## Resolved decisions

The initial decisions remain, with ADR-0009's explicit boundary refinements taking
precedence over ambiguous earlier prose.

| Question | Decision | Where |
| --- | --- | --- |
| Hard vs soft delete | **Hard delete** — `Closed` already covers "keep but deactivate" | [ADR-0006](../architecture/adr/0006-hard-delete.md) |
| Required-field set | First/Last/Email/CourseName required; Phone/Location/Message optional | [ADR-0005](../architecture/adr/0005-layered-service-dto.md) |
| Status transitions | **Free-form**; required, defined, name-only input; same-status no-op | [ADR-0008](../architecture/adr/0008-free-form-status-transitions.md), [ADR-0009](../architecture/adr/0009-testable-boundary-contracts.md) |
| Staff authentication | **Out of scope**; open endpoints are not safe for real data | [C7](../architecture/contracts.md#c7-web-ui-and-hosting) |
| `Id` strategy | **Int identity / autoincrement** | [ADR-0003](../architecture/adr/0003-efcore-sqlite-and-sql-script.md) |
| Validation | DataAnnotations plus explicit wire-value checks | [ADR-0005](../architecture/adr/0005-layered-service-dto.md), [C1–C3](../architecture/contracts.md) |
| CRM delivery | Awaited and bounded after commit; no durable outbox | [C5–C6](../architecture/contracts.md#c5-commit-boundary-cancellation-and-crm-delivery) |
| Test workflow | Red → green → refactor for each behavior, not optional tests at the end | [TDD extension](../testing/tdd-plan.md) |

See the full architecture and traceability in [`../architecture/`](../architecture/)
(open `index.html`), and the ADR log in
[`../architecture/adr/`](../architecture/adr/).
