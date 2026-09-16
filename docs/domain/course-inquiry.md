# Domain: Course Inquiry

The bounded context is a single internal tool: staff at Merrithew triage **course
registration inquiries** that visitors submit from the public website, and
optionally push them onward to an external business system (a CRM).

> **Source of truth.** No application code exists yet — planning is complete and
> implementation is next. Every rule below is traceable to the assessment
> specification (`option-1-course-inquiry-dashboard.md`), cited as `spec:<line>`.
> Points the spec left open were decided in the planning session (2026-09-16) and
> now link to the [ADR](../architecture/adr/) that records the reasoning; see the
> [Resolved decisions](#resolved-decisions) table below.

## Vocabulary

| Term | Means here | Does *not* mean |
| --- | --- | --- |
| **Inquiry** / CourseInquiry | A visitor's expression of interest in a course, captured from the website form. It is a *lead*, not a commitment. | A registration or enrolment — that happens in the external system, not here. Also not a support ticket. |
| **Visitor** | The public, unauthenticated person who submits an inquiry. | A staff user. |
| **Staff** | An internal user who views, filters, and advances inquiries via the admin list. | The visitor. (No authentication is implemented in this assessment; the staff endpoints are open. Access control is addressed in `written-answers.md` (Security), `spec:98`.) |
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
| `Email` | Visitor's email | **Format must be validated** (`spec:44`). Also the key for duplicate detection (`spec:59`). |
| `Phone` | Visitor's phone | Optional (see the required-field rule below). |
| `CourseName` | The course the visitor is asking about | Free text in the spec; not a foreign key to a course catalogue. |
| `PreferredLocation` | Where the visitor would want to take the course | " |
| `Message` | Free-text note from the visitor | Optional (see the required-field rule below). |
| `Status` | Workflow state (see Lifecycle) | Defaults to `New` (`spec:45`). |
| `CreatedDate` | When the inquiry was received | **Set automatically** by the system, not the client (`spec:46`). |
| `UpdatedDate` | When the inquiry last changed | **Set automatically** on every change (`spec:46`). |

Field **types**: `Id` is an int identity ([ADR-0003](../architecture/adr/0003-efcore-sqlite-and-sql-script.md));
`Status` binds to an enum ([ADR-0008](../architecture/adr/0008-free-form-status-transitions.md));
`CreatedDate`/`UpdatedDate` are `DateTime` (UTC); the rest are strings. Exact
lengths are set on the entity during implementation.

## Lifecycle

The spec calls these **"Suggested statuses"** (`spec:27`) and describes the
intent as staff advancing an inquiry "as it progresses" (`spec:7`). The natural
forward progression:

```
New ──contact──> Contacted ──> Pending ──> Registered ──> Closed
                                                             ▲
 (any state) ─────────────── delete/archive ────────────────┘
```

- **`New`** — just received; the default for every created inquiry (`spec:45`).
- **`Contacted`** — staff have reached out to the visitor.
- **`Pending`** — awaiting something (visitor decision, payment, a course date).
- **`Registered`** — the visitor has enrolled (the successful outcome).
- **`Closed`** — no further action; also the target of a soft-delete/archive
  (`spec:37`, `spec:39`).

**Transitions are free-form** ([ADR-0008](../architecture/adr/0008-free-form-status-transitions.md)):
any valid status may be set in any order, and staff can correct in either
direction; an unknown value is rejected with a 400. `PUT /api/inquiries/{id}/status`
(`spec:36`) is the only mutation path for status. The spec presents the five
states as a *suggestion*, not a mandated state machine, so no progression is
enforced.

## Rules

Each rule cites the spec line that mandates it. None are enforced in code yet —
this section is the checklist the implementation and its tests must satisfy.

- **Default status is `New`.** A newly created inquiry always starts at `New`,
  regardless of client input. (`spec:45`)
- **`CreatedDate` and `UpdatedDate` are system-assigned.** The client never sets
  them; `UpdatedDate` moves on every mutation. (`spec:46`)
- **Email must be a valid format.** Rejected with an appropriate error otherwise.
  (`spec:44`, `spec:47`)
- **Required fields must be present.** `FirstName`, `LastName`, `Email`, and
  `CourseName` are required; `Phone`, `PreferredLocation`, and `Message` are
  optional. Enforced with DataAnnotations on the request DTO
  ([ADR-0005](../architecture/adr/0005-layered-service-dto.md)). (`spec:44`)
- **Invalid input and missing records return appropriate errors.** e.g. 400 for
  validation failure, 404 for an unknown `Id`. (`spec:47`)
- **Status is only changed through the dedicated endpoint.** Not via a general
  update. (`spec:36`)
- **Delete is a hard delete.** The row is removed permanently
  ([ADR-0006](../architecture/adr/0006-hard-delete.md)); the `Closed` status
  covers the "keep but deactivate" case. (`spec:37`, `spec:39`)
- **CRM sync must not log sensitive data.** The simulated integration logs that a
  sync was attempted and its success/failure, without exposing sensitive fields.
  (`spec:78`–`spec:80`)

## Resolved decisions

The open questions from the planning session (2026-09-16) are now settled. Each
links to the ADR that records the rationale, options considered, and consequences.

| Question | Decision | Where |
| --- | --- | --- |
| Hard vs soft delete | **Hard delete** — `Closed` already covers "keep but deactivate" | [ADR-0006](../architecture/adr/0006-hard-delete.md) |
| Required-field set | First/Last/Email/CourseName required; Phone/Location/Message optional | [ADR-0005](../architecture/adr/0005-layered-service-dto.md) |
| Status transitions | **Free-form** + enum validation | [ADR-0008](../architecture/adr/0008-free-form-status-transitions.md) |
| Staff authentication | **Not implemented**; discussed in `written-answers.md` (Security) | — |
| `Id` strategy | **Int identity / autoincrement** | [ADR-0003](../architecture/adr/0003-efcore-sqlite-and-sql-script.md) |
| Validation | DataAnnotations on DTOs | [ADR-0005](../architecture/adr/0005-layered-service-dto.md) |

See the full architecture and traceability in [`../architecture/`](../architecture/)
(open `index.html`), and the ADR log in
[`../architecture/adr/`](../architecture/adr/).
