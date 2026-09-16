# Architecture

> **Status: target design (greenfield).** No application code exists yet. This
> folder captures the intended design, refined by the boundary review and TDD
> extension. Verifications are all `planned`; components describe intended, not
> existing, code. ADRs 0001–0008 remain historical records; ADR-0009 clarifies
> ambiguous boundaries without changing the single-process topology.

## Read the design and implementation plan

- [Domain specification](../domain/course-inquiry.md): vocabulary and business rules.
- [Boundary contracts C1–C8](contracts.md): input/wire contracts, paging, no-op
  updates, commit/cancellation semantics, CRM/privacy, UI races, and SQL behavior.
- [TDD extension](../testing/tdd-plan.md): red → green → refactor across every
  committed behavior, with [backend cases](../testing/backend-cases.md) and
  [frontend/hosting/SQL cases](../testing/frontend-and-sql-cases.md).
- [Implementation checklist](../../TODO.md): phase scope and completion gates.

The model owns topology and REQ/VER allocation; the domain and boundary documents
own semantics. Planned verification coverage is **not** passing test evidence.

## Interactive diagrams

The diagrams are a set of linked, offline HTML pages generated from
[`model.json`](model.json) — the topology and traceability source of truth. **Open
[`index.html`](index.html)** in a browser. Generated HTML and `data/model.*` are
outputs; edit the source model, not these copies.

| Page | Shows |
| --- | --- |
| `index.html` | Context overview: actors, the system boundary, the five subsystems |
| `decomposition.html` | System → subsystem → component tree |
| `traceability.html` | Requirements → verifications (the V), coverage stats, gap report |
| `sub--*.html` | One page per subsystem, internals in focus |
| `flow--flow-submit.html` | Inquiry intake + CRM sync (the path that pays for the system) |
| `flow--flow-triage.html` | Staff filter + status update |

Reader controls: scroll to zoom, drag to pan, click any box to inspect it, `/`
search, `t` trace mode, `a` to annotate (annotations export as JSON in the
`model.json` shape and can be pasted back in), `0` fit.

To regenerate after editing `model.json`, use the repository entrypoint
[`scripts/render_architecture.py`](../../scripts/render_architecture.py) with
Python 3.12+. It validates and invokes the installed external `system-diagrams`
skill, then corrects its static “components built” / “allocated and verified”
labels: those counts describe definitions, not implementation or passing tests.
The page displays the model's actual passing-verification count separately.

```bash
uv run --python 3.12 scripts/render_architecture.py
```

The external skill defaults to `$HOME/.claude/skills/system-diagrams`; pass
`--skill-dir /path/to/system-diagrams` if installed elsewhere. A fresh clone can
open the committed offline diagrams without Python or the skill. Regeneration
requires both; the wrapper fails explicitly if the upstream label template
changes instead of silently reintroducing a false evidence claim.

## The shape in one paragraph

One ASP.NET Core (.NET 10) app. A **Web UI** subsystem (Razor Pages shell + a
React/TypeScript island) and the external public form call the **HTTP API**
subsystem (five REST endpoints, DTO validation, ProblemDetails, OpenAPI).
Controllers delegate to **Application** (`InquiryService`), which owns the
business rules and talks to **Persistence** (EF Core → SQLite). After commit,
the service awaits **CRM Integration** through `ICrmClient`; the simulation uses
bounded retry and safe metadata-only logs. CRM errors are isolated after commit,
not confused with a failed database write. The public form itself is not built.

## Subsystems

| Subsystem | Responsibility | Key components |
| --- | --- | --- |
| Web UI | What staff touch | Razor Pages shell, React island |
| HTTP API | REST surface + validation + errors + docs | Inquiries controller, request validation, error handling, OpenAPI/Swagger |
| Application | Business rules & orchestration | `InquiryService` |
| Persistence | Durable store, source of truth | `AppDbContext`, SQLite database |
| CRM Integration | Best-effort external sync | Simulated CRM client (Polly retry), redacted sync log |

## The load-bearing rule

**Persist first, sync second.** Commit the inquiry before attempting CRM work.
Catch failures only at the post-commit CRM boundary. A usable connection receives
the created inquiry even when CRM fails; a disconnected caller may not learn that
its write committed. Retrying that POST can create another inquiry. A process
crash can lose CRM delivery because there is no outbox. See
[contract C5](contracts.md#c5-commit-boundary-cancellation-and-crm-delivery) and
[`flow--flow-submit.html`](flow--flow-submit.html).

The CRM call is awaited and can add latency; “does not prevent storage” is not
“never blocks the response.” Cancellation and timeouts are cooperative. Stable
pagination is not snapshot isolation, and UTC timestamps are not concurrency
tokens. These limits are explicit so tests do not promise stronger guarantees.

## Decision records

The `why` behind the design lives in [`adr/`](adr/):

| ADR | Decision |
| --- | --- |
| [0001](adr/0001-record-architecture-decisions.md) | Record architecture decisions as ADRs |
| [0002](adr/0002-dotnet-10-aspnet-core.md) | .NET 10 (LTS) + ASP.NET Core (Web API + Razor Pages) |
| [0003](adr/0003-efcore-sqlite-and-sql-script.md) | EF Core + SQLite runtime store; SQL Server `database.sql`; int keys |
| [0004](adr/0004-razor-shell-react-island.md) | Razor Pages shell hosting a React + TypeScript island |
| [0005](adr/0005-layered-service-dto.md) | Layered controller → service → EF Core, DTOs, DataAnnotations |
| [0006](adr/0006-hard-delete.md) | Hard delete |
| [0007](adr/0007-crm-port-retry-logging.md) | CRM via `ICrmClient` port, Polly retry, redacted logging |
| [0008](adr/0008-free-form-status-transitions.md) | Free-form status transitions with enum validation |
| [0009](adr/0009-testable-boundary-contracts.md) | Explicit boundary contracts and comprehensive test-first development |

## Related

- Domain model & rules: [`../domain/course-inquiry.md`](../domain/course-inquiry.md)
- The assessment spec: [`../../option-1-course-inquiry-dashboard.md`](../../option-1-course-inquiry-dashboard.md)
