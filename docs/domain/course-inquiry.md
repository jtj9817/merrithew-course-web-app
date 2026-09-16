# Domain: Course Inquiry

The bounded context is a single internal tool: staff at Merrithew triage **course
registration inquiries** that visitors submit from the public website, and
optionally push them onward to an external business system (a CRM).

> **Source of truth.** No application code exists yet — this project is in its
> planning phase. Every rule below is traceable to the assessment specification
> (`option-1-course-inquiry-dashboard.md`), cited as `spec:<line>`. Where the
> spec is silent, that is marked **(planning decision)** rather than guessed —
> those are open questions for the planning session, not settled facts.

## Vocabulary

| Term | Means here | Does *not* mean |
| --- | --- | --- |
| **Inquiry** / CourseInquiry | A visitor's expression of interest in a course, captured from the website form. It is a *lead*, not a commitment. | A registration or enrolment — that happens in the external system, not here. Also not a support ticket. |
| **Visitor** | The public, unauthenticated person who submits an inquiry. | A staff user. |
| **Staff** | An internal user who views, filters, and advances inquiries via the admin list. | The visitor. (Whether staff access is authenticated is a **planning decision** — the spec lists auth only as a bonus / written-question topic, `spec:98`, `spec:147`.) |
| **Status** | The workflow state of an inquiry as staff process it. | The delivery state of a course. |
| **CRM sync** | A *simulated* push of an inquiry to an external CRM. No real CRM or API key is involved. | A live integration. (`spec:75`) |

## Entities

### CourseInquiry

The only entity in the system. Represents one inquiry submitted by one visitor
about one course. Fields, as enumerated by the spec (`spec:15`–`spec:25`):

| Field | Meaning | Notes |
| --- | --- | --- |
| `Id` | Unique identifier | Type/generation is a **planning decision** (e.g. int identity vs GUID). |
| `FirstName` | Visitor's given name | Required-field validation applies (`spec:44`); exact required set below. |
| `LastName` | Visitor's family name | " |
| `Email` | Visitor's email | **Format must be validated** (`spec:44`). Also the key for duplicate detection (`spec:59`). |
| `Phone` | Visitor's phone | Optionality is a **planning decision** (spec does not mark it required). |
| `CourseName` | The course the visitor is asking about | Free text in the spec; not a foreign key to a course catalogue. |
| `PreferredLocation` | Where the visitor would want to take the course | " |
| `Message` | Free-text note from the visitor | Optionality is a **planning decision**. |
| `Status` | Workflow state (see Lifecycle) | Defaults to `New` (`spec:45`). |
| `CreatedDate` | When the inquiry was received | **Set automatically** by the system, not the client (`spec:46`). |
| `UpdatedDate` | When the inquiry last changed | **Set automatically** on every change (`spec:46`). |

Field **types** are deliberately not fixed here — assigning them is a design step
for the data-model decision in planning, not something the spec dictates.

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

**Transition enforcement is a planning decision.** The spec does not say whether
status changes are constrained to this forward order or whether staff may set any
status at any time. `PUT /api/inquiries/{id}/status` (`spec:36`) is the only
mutation path for status. *Why the five states in this order: unknown — the spec
presents them as a suggestion, not a mandated state machine. Confirm the intended
rigidity during planning.*

## Rules

Each rule cites the spec line that mandates it. None are enforced in code yet —
this section is the checklist the implementation and its tests must satisfy.

- **Default status is `New`.** A newly created inquiry always starts at `New`,
  regardless of client input. (`spec:45`)
- **`CreatedDate` and `UpdatedDate` are system-assigned.** The client never sets
  them; `UpdatedDate` moves on every mutation. (`spec:46`)
- **Email must be a valid format.** Rejected with an appropriate error otherwise.
  (`spec:44`, `spec:47`)
- **Required fields must be present.** The spec mandates required-field validation
  (`spec:44`) but does **not** enumerate the required set. Proposed default:
  `FirstName`, `LastName`, `Email`, `CourseName` required; `Phone`,
  `PreferredLocation`, `Message` optional — **to be confirmed in planning.**
- **Invalid input and missing records return appropriate errors.** e.g. 400 for
  validation failure, 404 for an unknown `Id`. (`spec:47`)
- **Status is only changed through the dedicated endpoint.** Not via a general
  update. (`spec:36`)
- **Delete may be hard or soft.** The spec permits either a hard delete or a
  soft-delete/archive (mark `Closed`/archived), and requires the choice to be
  explained in the README. (`spec:37`, `spec:39`) *This is the first planning
  decision to record.*
- **CRM sync must not log sensitive data.** The simulated integration logs that a
  sync was attempted and its success/failure, without exposing sensitive fields.
  (`spec:78`–`spec:80`)

## Open questions for planning

Collected from the **(planning decision)** flags above, in rough priority order:

1. Hard delete vs soft-delete/archive — and if soft, is "archived" distinct from `Closed`?
2. The exact required-field set and each field's type/length constraints.
3. Whether status transitions are constrained (state machine) or free-form.
4. Whether staff access is authenticated at all for this assessment.
5. `Id` strategy (int identity vs GUID) and its knock-on effects for the SQL scripts.
