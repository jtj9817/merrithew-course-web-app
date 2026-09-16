# TDD extension — Course Inquiry Dashboard

> **Status: planned, not executed.** There is no application, solution, package
> manifest, or executable test suite yet. This document extends [TODO.md](../../TODO.md),
> replacing its old “tests at the end / remaining tests as time allows” approach.
> It does not claim that any test already exists or passes.

## Scope and source of truth

Develop **every committed behavior** using red → green → refactor. This covers
all five endpoints, domain rules, SQLite persistence/migrations, CRM resilience
and privacy, React interactions, Razor/asset hosting, OpenAPI, and the companion
SQL Server script. “Everything” means behavior and integration boundaries, not
one test per method, 100% line coverage, or tests that freeze DTO copies and
framework wording. Written answers, setup instructions, and visual accessibility
still need human verification; automation does not replace those deliverables.

Read in this order:

1. [Domain rules](../domain/course-inquiry.md) and
   [contracts C1–C8](../architecture/contracts.md): intended outcomes.
2. [ADR-0009](../architecture/adr/0009-testable-boundary-contracts.md): why the
   test scope and previously ambiguous boundary choices changed.
3. [Backend cases](backend-cases.md) and
   [frontend, hosting, and SQL cases](frontend-and-sql-cases.md): concrete
   arrange/act/assert specifications, each linked to REQ/VER IDs.
4. [Architecture model](../architecture/model.json): allocation and verification
   status. Its diagram coverage means “a verification is planned,” not “tested.”

All catalog cases are required for this extension. Select a focused case for
one cycle; do not silently omit the rest because the assessment's minimum is
only one test. Change a contract deliberately before changing its expected
result. No production auth, visitor form, UI create/delete, real CRM, outbox,
idempotency key, or optimistic locking is added.

## The repeatable cycle

For each unchecked behavior in TODO.md:

1. **Select:** choose the catalog case(s) and record their IDs with the slice.
   Identify the consumer-visible outcome, failure mode, and smallest boundary
   that can actually prove it. Agree any unresolved behavior before coding it.
2. **Red:** write the smallest meaningful test and run it. Observe the expected
   assertion failure because the behavior is absent or wrong. A missing SDK,
   bad fixture, failed restore, noncompiling test, or zero discovered tests is
   not the red proof. Add only enough type/host scaffolding to reach the real
   assertion; do not implement the behavior before observing red.
3. **Green:** implement the smallest complete production path that makes this
   assertion pass. Keep production dependencies real; substitute only the
   controlled external/failure boundary in the test. Do not leave runtime stubs
   or no-op fallbacks as a finished slice.
4. **Refactor:** remove duplication and clarify naming while the focused tests
   stay green. Do not add a repository, policy hierarchy, or helper API solely
   to make a test easy to mock. Preserve observable contracts, not call counts
   except where attempts/side effects are themselves the required behavior.
5. **Integrate:** run the relevant unit and integration group, then exercise the
   changed path in the running application. UI work includes a real-browser
   check. Add the remaining edge cases for the same boundary before closing it.
6. **Record:** retain case IDs, red failure summary, green command/result,
   runtime/provider versions, and any manual evidence in the commit/PR notes.
   Commit a coherent green slice. Only mark its model verification passing when
   every mapped case and required supplemental check has evidence.

Do not write broad snapshots, assert private method wiring, test copied SQL,
or duplicate the same validation permutations at every layer. Unit tables can
cover independent field/length rules; HTTP tests prove representative binding
and validation composition that unit tests cannot exercise. A test with no
plausible behavioral regression to catch should not be retained.

## Test boundaries and planned tooling

| Lane | Actual subject | Controlled boundaries | Planned tooling / location |
| --- | --- | --- | --- |
| .NET unit | DTO validation, status wire conversion, real CRM retry/timeout and safe log policy | Time, deterministic simulated attempt, captured log sink; no database or HTTP host | xUnit; `tests/CourseInquiryDashboard.Tests/Unit/` |
| .NET integration | Service + EF/SQLite; full HTTP routing/JSON/validation/service/store; startup; Razor/OpenAPI/static assets | CRM port for API/service tests, time, database failure injection; keep the layer being tested real | xUnit, `Microsoft.AspNetCore.Mvc.Testing` 10.x, SQLite provider 10.x; `tests/CourseInquiryDashboard.Tests/Integration/` |
| Frontend unit | Pure client error/data interpretation where such logic exists | Supplied response inputs | Vitest; `frontend/src/**/*.test.ts` |
| Frontend component integration | Real island/components, hooks, DOM, and user actions together | Controlled fetch responses and scheduling, not mocked component internals | Vitest + jsdom + React Testing Library + user-event; `frontend/src/**/*.test.tsx` |
| SQL Server integration | The shipped T-SQL DDL, sample data, and the three actual query bodies | Disposable database and fixed report time | xUnit + `Microsoft.Data.SqlClient`; `tests/CourseInquiryDashboard.Tests/SqlServer/` |
| Supplemental acceptance | Built app in a real browser; setup/docs/written answers | Synthetic records only | Browser walkthrough, not a replacement for unit/integration tests |

Keep the already planned single .NET test project, separating directories and
mutually exclusive `Category=Unit`, `Category=Integration`, `Category=SqlServer`
traits. Assign each .NET test its `CaseId` trait; include the case ID in frontend
`describe`/test names. All tests must be discoverable and isolated. Pin compatible
stable package versions during scaffolding; no package installation is implied
by this document. The frontend `test` script will run `vitest run` (non-watch).

An `InquiryService` test that uses SQLite is an **integration test**, even if it
calls the service directly. Do not use EF's InMemory provider or a mocked DbSet
as evidence for filtering, transactions, constraints, or migrations. SQLite is
the actual runtime provider here, not a SQL Server substitute. A direct
controller invocation cannot test `[ApiController]`, binding, routing, middleware,
or serialization; those assertions go through `WebApplicationFactory<Program>`.

## Fixture and isolation requirements

- Use only synthetic data. Keep a small valid DTO builder and override only the
  field relevant to a case. Give equal-time pagination rows known IDs/statuses
  so expected order and filtered totals are exact, not merely nonempty.
- Own a distinct SQLite database per test. For ordinary sequential tests, keep
  an opened `Data Source=:memory:` connection alive until all contexts/hosts
  using it are disposed. Apply the real migrations, not `EnsureCreated` as a
  substitute. Resolve writes and verification reads in different contexts so
  the change tracker cannot manufacture a successful persistence assertion.
- **Commit visibility, restart, and overlapping connections require a unique
  temporary file database** and independent connections. A second context on
  the same open connection can see an uncommitted transaction and is not proof
  of “committed before CRM.” Do not share a DbContext or connection concurrently.
  Release hosts, contexts, readers, and pools before removing owned files.
- Replace test database/clock/CRM registrations before the app's migration/startup
  code runs. Never migrate the developer's database. Test both Development and
  Production error paths explicitly rather than accepting the test host default.
  Disable client auto-redirects when verifying status codes and use an HTTPS
  test base address where HTTPS-redirection middleware is present.
- Inject `TimeProvider` for UTC business time and controlled timer behavior for
  Polly. Exercise the real production pipeline, not a fake that returns the
  desired attempt count. Advance virtual time at synchronization barriers;
  don't replace backoff with zero and claim delays were tested. Ensure timers
  are registered before advancing; use finite test watchdogs for deadlocks,
  not stopwatch-based pass/fail thresholds.
- Use gates/signals to place cancellation before an insert or after a confirmed
  commit, and to order competing writes or responses. Client-side cancellation
  alone cannot prove which side of the commit boundary the server reached.
  General transport-loss outcomes are documented limitations, not assertions
  that the row must be absent.
- Capture all relevant logger state, scopes, formatted output, and exceptions.
  Inject distinctive sensitive sentinels into visitor fields and exception
  messages. Inspect the real configured sink, including middleware and enabled
  Polly/EF logging; passing only a standalone masking-function test is insufficient.
- Frontend tests restore fetch mocks, timers, and DOM after each case. Reordering
  responses must be deliberate, with promises released by the test. No external
  service, mutable module singleton, fixed port, or shared fake clock leaks into
  another test. Use accessible roles/labels, not component-private state or CSS.
- SQL Server tests use `SQLSERVER_TEST_CONNECTION_STRING` from the environment
  for an explicitly disposable test server. Create uniquely named databases;
  never drop/reset a supplied non-test database. Dispose connections before
  dropping the owned database; do not print the connection string. Run the
  shipped script and query sections, not a reimplementation inside assertions.

## Dependency-ordered TDD slices

The phases in TODO.md remain useful scope buckets; this table overrides their
old test-after-code order. A vertical slice may span adjacent phases so its HTTP
assertion can go red before the endpoint/service behavior is implemented.

| Slice / TODO phase | Write and observe red first | Implement to green; evidence needed to exit |
| --- | --- | --- |
| Harness / 0 | Discover a real catalog test and reach its intended failure | .NET/xUnit and frontend test tooling, isolated fixtures, controlled clock/CRM; empty-suite success is not feature evidence |
| Intake contract / 2 | `UT-VAL` and relevant `IT-API`: missing/blank/length/email, malformed input, ignored overposting | DTOs/JSON settings and validation pipeline; invalid input has no row or CRM side effect |
| Durable creation / 1, 3, 5 | `IT-DATA`, `IT-APP`, `IT-API`: migration, exact UTC/defaults, independent-connection commit visibility, POST → GET/list | Real migration, entity mapping, service create and HTTP resource response; rollback/failure and repeat submissions covered |
| Triage / 3, 5 | Status/no-op/invalid values, filter-before-page, tied ordering, bounds, missing records, hard delete and controlled races | Full query/update/delete paths, including correct errors and timestamps; never lose Closed rows through an implicit archive filter |
| CRM / 4 | `UT-CRM` plus `IT-APP`/`IT-API`: retries/exhaustion, permanent errors, cancellation/timeout, complete sink privacy | Real Polly simulation inside the post-commit boundary; no durable row lost, no PII or raw error leaked |
| SQL deliverable / 1 | `IT-SQL`: empty-database DDL/sample execution, fixed-time boundaries, exact count/duplicate results | Actual SQL Server script and query execution; do not check this slice off with SQLite evidence |
| Host/API documentation / 5, 6 | OpenAPI integration and `IT-HOST` after a production frontend build | All five documented operations/DTO outcomes; Razor manifest assets load from the backend without Vite |
| Interactive dashboard / 6 | `UT-UI`, `IT-UI`: observable interactions, late responses, empty/error recovery, focus and safe rendering | Real React island behavior; component integration green plus supplemental browser acceptance |
| Regression gate / 7 | All selected catalog tests already exist from their slices | Run the whole applicable suite, including SQL Server for release; Phase 7 is not where testing begins |
| Documentation and closeout / 8, 9 | Review actual commands and contracts against implementation and test evidence | Setup verified from a clean checkout; written answers complete; model updated only for genuinely passing evidence |

## Execution commands after scaffolding

These are the **target commands to implement**, not commands that work in this
planning-only checkout. Run from the repository root unless noted. Add the
packages/scripts/traits above before using them; a filtered run discovering zero
tests must fail the verification gate rather than count as success.

```bash
# Focused .NET red/green cycle (use the selected catalog CaseId).
dotnet test --filter 'CaseId=IT-APP-001'

# Independent lanes; frontend install is required first.
dotnet test --filter 'Category=Unit'
dotnet test --filter 'Category=Integration'
pnpm --dir frontend test

# Production assets must exist before IT-HOST runs.
pnpm --dir frontend build
dotnet test --filter 'Category!=SqlServer'

# Required full-script evidence on a disposable SQL Server, separately provisioned.
# Supply SQLSERVER_TEST_CONNECTION_STRING securely in the environment.
dotnet test --filter 'Category=SqlServer'
```

The standalone Integration lane also needs the production asset build when
host cases are selected. A future CI job must restore/install dependencies,
build the frontend, then run the .NET integration and frontend suites; no
special CI configuration exists yet. Run existing format/lint/build checks once
at the end of a completed implementation slice, not while concurrent edits are
in flight.

SQL Server is optional for the fast local feedback loop, **required before
claiming the SQL deliverable fully verified**. Missing connectivity/credentials
or an unavailable engine must make the explicitly selected SQL lane fail with a
clear prerequisite diagnostic, never silently skip to green. An infrastructure
block does not waive the cases or mark VER-DATA-002 passing. No real CRM or
external credentials are needed for any application test.

## Requirement-to-evidence index

Exact case mappings are in each catalog row. This index covers every model
requirement, including the additions for gaps found in the review.

| REQ | VER | Required evidence location |
| --- | --- | --- |
| REQ-SYS-001 | VER-SYS-001 | `IT-UI`, `IT-HOST`, and supplemental browser triage |
| REQ-SYS-002 | VER-SYS-002 | `IT-API` create/read/list; `IT-APP` definite failure and commit boundary |
| REQ-SYS-003 | VER-SYS-003 | `IT-APP`/`IT-API` post-commit CRM failure and cancellation |
| REQ-SYS-004 | VER-SYS-004 | `UT-CRM` plus configured-host `IT-API` log sink privacy |
| REQ-SYS-005 | VER-SYS-005 | `IT-API` validation, missing records, sanitized failures |
| REQ-UI-001 | VER-UI-001 | `IT-UI` list/filter/detail/status/feedback plus browser walkthrough |
| REQ-UI-002 | VER-UI-002 | `UT-UI`, `IT-UI`, `IT-HOST`: async state, safe accessible UI, real assets |
| REQ-API-001 | VER-API-001 | `IT-API` five endpoint workflows |
| REQ-API-002 | VER-API-002 | `UT-VAL` rules plus HTTP binding/validation cases |
| REQ-API-003 | VER-API-003 | `IT-API` missing/invalid IDs, repeat delete, race response mapping |
| REQ-API-004 | VER-API-004 | `IT-APP`/`IT-API` stable filtered pages, limits, empty/out-of-range pages |
| REQ-API-005 | VER-API-005 | `IT-API` OpenAPI contract; manual Swagger create/update |
| REQ-APP-001 | VER-APP-001 | `IT-APP`/`IT-API` forced defaults, UTC, no-op and clock cases |
| REQ-APP-002 | VER-APP-002 | `UT-VAL`, `IT-APP`, `IT-API` legal/illegal/omitted status |
| REQ-APP-003 | VER-APP-003 | `IT-APP`/`IT-API` irreversible delete versus retained Closed |
| REQ-DATA-001 | VER-DATA-001 | `IT-DATA` persistence and UTC representation; `IT-API` read-after-create |
| REQ-DATA-002 | VER-DATA-002 | `IT-SQL` DDL, samples, and all three report queries on SQL Server |
| REQ-DATA-003 | VER-DATA-003 | `IT-DATA` actual migration, restart, fatal startup failure |
| REQ-CRM-001 | VER-CRM-001 | `IT-APP`/`IT-API` stored inquiry survives CRM errors |
| REQ-CRM-002 | VER-CRM-002 | `UT-CRM` real pipeline transient attempts, delays, exhaustion/permanent failure |
| REQ-CRM-003 | VER-CRM-003 | `UT-CRM`/`IT-API` attempt/outcome logs without sensitive payloads |
| REQ-CRM-004 | VER-CRM-004 | `UT-CRM` timeouts/backoff cancellation; `IT-APP` post-commit durability |

## Completion and evidence policy

A slice is complete only when its cases have observed red/green evidence, related
regressions pass, and the actual changed surface has been exercised. The final
gate requires every catalog case, the SQL Server lane, the browser walkthrough,
and the assessment's non-code deliverables. Report skipped/blocked work by case
ID and reason; do not relabel it as passed or weaken the plan.

Keep architecture `status: planned` while only these documents exist. A future
`passing` verification needs implementation test paths plus a recorded command,
result, and revision in its evidence/procedure. A verification aggregating unit,
integration, and manual cases is passing only when **all** required evidence is
current. Redesigns that invalidate evidence return the verification to planned
until rerun. Regenerate the architecture pages after model updates.

## Primary guidance used

- Microsoft Learn: [ASP.NET Core integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0) for WebApplicationFactory, actual HTTP-pipeline testing, and explicit test-host environments. Its project-separation recommendation is implemented here as category/directory separation in the small, already planned test project.
- Microsoft Learn: [choosing an EF testing strategy](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy) and [SQLite in-memory connection lifetime](https://learn.microsoft.com/en-us/ef/core/testing/testing-without-the-database#sqlite-in-memory). SQLite tests do not establish T-SQL correctness.
- Microsoft Learn: [validation](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation?view=aspnetcore-10.0), [transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions), and [concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) ground the boundary cases.
- Polly: [retry](https://www.pollydocs.org/strategies/retry.html) and [timeout](https://www.pollydocs.org/strategies/timeout.html) distinguish attempts, retries, and cooperative cancellation.
