# Architecture

> **Status: target design (greenfield).** No application code exists yet. This
> folder captures the design agreed in the planning session on **2026-09-16**.
> Verifications are all `planned`; components describe intended, not existing,
> code.

## Interactive diagrams

The diagrams are a set of linked, offline HTML pages generated from
[`model.json`](model.json) — the single source of truth. **Open
[`index.html`](index.html)** in a browser.

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

To regenerate after editing `model.json`:

```bash
python3 <system-diagrams-skill>/scripts/validate_model.py docs/architecture/model.json
python3 <system-diagrams-skill>/scripts/render.py     docs/architecture/model.json -o docs/architecture
```

> Note: the skill's `render.py` needs Python 3.12+ as shipped (it uses a
> 3.12 f-string feature). On Python 3.10/3.11, either run it under
> `uv run --python 3.12 …` or apply the two-line backslash-in-f-string fix.

## The shape in one paragraph

One ASP.NET Core (.NET 10) app. A **Web UI** subsystem (Razor Pages shell + a
React/TypeScript island) and, for a visitor, the public form both call the
**HTTP API** subsystem (five REST endpoints, DataAnnotations validation, error →
ProblemDetails, OpenAPI). Controllers are thin and delegate to the
**Application** subsystem (`InquiryService`), which owns the business rules and
talks to the **Persistence** subsystem (EF Core → SQLite). After an inquiry is
persisted, the service calls the **CRM Integration** subsystem through the
`ICrmClient` port; the simulated client retries with backoff and logs each
attempt with sensitive fields redacted — and can never fail the create.

## Subsystems

| Subsystem | Responsibility | Key components |
| --- | --- | --- |
| Web UI | What staff touch | Razor Pages shell, React island |
| HTTP API | REST surface + validation + errors + docs | Inquiries controller, request validation, error handling, OpenAPI/Swagger |
| Application | Business rules & orchestration | `InquiryService` |
| Persistence | Durable store, source of truth | `AppDbContext`, SQLite database |
| CRM Integration | Best-effort external sync | Simulated CRM client (Polly retry), redacted sync log |

## The load-bearing rule

**Persist first, sync second.** The inquiry is written and committed *before* the
CRM sync is attempted, and a sync failure is caught by the service rather than
propagated. This is what lets two requirements hold at once: an inquiry is never
lost (REQ-SYS-002) *and* a CRM outage never blocks a submission (REQ-SYS-003). See
[`flow--flow-submit.html`](flow--flow-submit.html).

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

## Related

- Domain model & rules: [`../domain/course-inquiry.md`](../domain/course-inquiry.md)
- The assessment spec: `../../option-1-course-inquiry-dashboard.md`
