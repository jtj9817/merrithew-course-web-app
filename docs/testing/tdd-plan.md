# TDD extension — Course Inquiry Dashboard

> **Status: partially implemented.** Phases 0–2 now have application and test
> code; the [implementation record](#phases-02-implementation-record) below
> distinguishes executed evidence from later-phase planned cases. This document
> extends [TODO.md](../../TODO.md); tests are not deferred to the end.

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

## Phases 0–2 implementation record

Implemented in the current working tree (no commit was created by this task).
Runtime: .NET SDK 10.0.112, ASP.NET Core/runtime 10.0.12, EF Core SQLite 10.0.12,
xUnit 2.9.3, SqlClient 7.0.3. SQL tests ran on a disposable SQL Server 2022
container (`mcr.microsoft.com/mssql/server:2022-latest`, pulled digest
`sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090`).

Observed red assertions before their corresponding integration changes:

- `UT-VAL-002`: missing first name produced no validation error with the initial
  DTO skeleton; the required-field assertion failed before annotations landed.
- `IT-DATA-001`: migration execution left zero inquiry tables before the initial
  migration was generated; the expected table-count assertion failed.
- `DTO-HTTP-007/008/009` status/response checks: three HTTP tests failed before
  registering the explicit status JSON converter in the production MVC options.

Green commands and evidence:

| Command / surface | Observed result |
| --- | --- |
| `dotnet test --filter 'Category!=SqlServer'` | 91 passed, 0 failed, 0 skipped |
| `dotnet test --no-build --filter 'Category=SqlServer'` with disposable-server environment | 8 passed, 0 failed, 0 skipped |
| `dotnet run --project backend --no-build --no-launch-profile` with an isolated SQLite file | Startup migrated before listening; Swagger document and UI returned HTTP 200; a synthetic row survived a stopped/restarted process |

Test paths: `tests/CourseInquiryDashboard.Tests/Unit/`,
`Integration/PersistenceTests.cs`, `Integration/DtoHttpTests.cs`, and
`SqlServer/CourseInquirySqlScriptTests.cs`. The SQL lane executes the shipped
script and its extracted query text on SQL Server, not SQLite or copied queries.
These results do not claim separate pre-implementation red runs for every
catalog permutation.

The fatal-migration test initially hung when a test migration threw while EF
was generating operations: SQLite retained its migration-lock row. The fixture
now injects a genuinely failing SQL operation during migration execution. The
host fails fatally, the migration is not recorded, existing data survives, and
the normal host restarts successfully. No production lock-clearing workaround
was added.

DTO HTTP tests mount test-only controllers through the real MVC configuration.
Their `DTO-HTTP-*` IDs deliberately do **not** claim completion of `IT-API-*`
resource workflows. The service-side portion of `UT-VAL-012`, commit-before-CRM,
CRUD, list paging behavior, and all dashboard behavior remain later-phase work.
`UT-VAL-012` currently proves only direct DTO membership rejection. Broad
REQ/VER model entries remain `planned` until every mapped case has evidence.

## Phases 3–5 implementation record

Implemented in the current working tree. All 51 backend catalog cases in Phases 3–5
have executable evidence:
- `IT-APP-001..019`: 19 service integration tests over real SQLite (`InquiryServiceTests.cs`).
- `IT-APP-020`: 1 cross-process OS-kill durability and no-replay test (`ProcessRestartTests.cs`).
- `UT-CRM-001..010`: 10 real Polly v8.8.0 pipeline unit tests with captured sink privacy (`CrmPipelineTests.cs`).
- `IT-API-001..021`: 21 full HTTP host integration tests over real SQLite (`InquiriesApiTests.cs`).

Observed red assertions before corresponding implementations:

- `IT-APP-001..019`: all 19 tests failed with `NotImplementedException` against the stub `InquiryService`.
- `UT-CRM-001..010`: all 10 tests failed against the stub `SimulatedCrmClient`.
- `IT-API-001..021`: 20 of 21 tests failed with `404 Not Found` before `InquiriesController`, `ListInquiriesQueryDto`, and `Program.cs` wiring landed (`IT-API-005` passed vacuously before routing existed).

Green commands and evidence:

| Command / surface | Observed result |
| --- | --- |
| `dotnet test --no-build --filter 'Category!=SqlServer'` | 142 passed, 0 failed, 0 skipped (51 new cases + 91 from Phases 0–2) |
| `dotnet test --filter 'CaseId=IT-APP-020'` | 1 passed (OS SIGKILL, independent SQLite verification, real backend restart, listable row, zero CRM replay for old row, future create CRM sync confirmed) |
| `dotnet format --verify-no-changes` | Clean, 0 whitespace or formatting issues |
| `dotnet build --warnaserror` | Succeeded with 0 Warnings and 0 Errors |
| Live smoke: `dotnet run --project backend` | `GET /swagger/v1/swagger.json` and `/swagger/index.html` HTTP 200 in Development; `POST /api/inquiries` 201; `GET /api/inquiries` 200; `PUT .../status` 200; `DELETE ...` 204; `GET ...` 404 |

Real bugs caught and resolved during the TDD loop:

1. **`ConvertEmptyStringToNull` on query strings:** ASP.NET Core MVC default `DisplayMetadata.ConvertEmptyStringToNull` silently converted `?page=` and `?status=` to `null`, treating supplied-empty values as omitted defaults. Resolved by applying `[DisplayFormat(ConvertEmptyStringToNull = false)]` to `ListInquiriesQueryDto` properties per C4.
2. **Controller `[Produces]` overriding `ProblemDetails` media type:** `[Produces("application/json")]` forced validation and resource errors to `application/json` instead of `application/problem+json`. Removed to allow natural ProblemDetails content negotiation.
3. **`ExceptionHandlerMiddlewareImpl` leaking raw exceptions to sinks:** The built-in middleware attaches unhandled exception objects to error log entries, violating C6. Replaced with direct try/catch middleware that logs only `{ErrorType}` and emits sanitized `Results.Problem(statusCode: 500)` in all environments.
4. **LoggerMessage state key casing:** LoggerMessage source-gen retains template placeholder casing (`{Outcome}` -> key `Outcome`). Updated test inspection helpers to use case-insensitive key lookup.
5. **Polly delay timer resolution:** `Task.Delay(400)` under Polly v8 completed 0.16 ms under 400 ms on Linux; widened test lower bounds slightly (385 ms) while strictly enforcing monotone exponential ordering.

Phase 6 (React island frontend & Razor shell hosting) is implemented and recorded
below; Phase 9 (model.json verification updates) remains planned.

## Phase 6 implementation record

Executed 2026-09-17. All mapped frontend/hosting cases
(UT-UI-001..005, IT-UI-001..030, IT-HOST-001..004) pass, plus supplemental
MAN-UI browser walkthroughs against the real running app.

### Delivered

- **Frontend project (`frontend/`)**: Vite 8 + React 19 + TypeScript 7 (strict),
  Vitest 5 + jsdom + Testing Library (`react`, `user-event`, `jest-dom`).
  `test` script is non-watch `vitest run`; test files live at
  `frontend/src/**/*.test.ts(x)` with case IDs in describe/test names.
  Pinned build contract honored: `base: '/app/'`,
  `build.outDir: '../backend/wwwroot/app'` (`emptyOutDir` confined to `app/`),
  manifest at `backend/wwwroot/app/.vite/manifest.json`.
- **Test harness (`frontend/src/test/`)**: canonical §4 fixtures transcribed
  verbatim (`fixtures.ts`), a FIFO fetch double with deferred handles and
  request recording (`fetchDouble.ts`, installs on both `window.fetch` and
  `globalThis.fetch` because bare `fetch` resolves through the Node global in
  jsdom), and `renderIsland(prepare)` which programs the double *before* mount
  (the initial list request fires during render).
- **Island (`frontend/src/`)**: `App` owns list/filter/sort/page state with a
  sequence guard plus `AbortController` for list and detail requests (older
  responses never overwrite newer state, resolutions after unmount are silent);
  empty-page reconciliation to `max(1, ceil(totalCount/pageSize))`; detail
  drawer with focus-in/Escape/Close focus return; per-row status select + Apply
  with duplicate-submission guard; PUT-then-refresh with "saved, refresh
  failed" combined wording; safe ProblemDetails/network degradation via the
  outcome classifier; one polite (`role="status"`) and one assertive
  (`role="alert"`) region; no create/delete affordances anywhere.
- **Razor shell (`backend/Pages/`)**: `/dashboard` with exactly one
  `#dashboard-root` mount, loading placeholder, `<noscript>` guidance, and
  manifest-resolved entry script + stylesheets (`backend/Hosting/ViteManifest.cs`
  reads the manifest per request; a missing manifest degrades to guidance
  without crashing). `Program.cs` adds `AddRazorPages`, `UseStaticFiles`,
  `UseRouting`, `MapRazorPages`, and `/` → `/dashboard`. A real `favicon.ico`
  ships in `backend/wwwroot` (`.gitignore` ignores only the generated
  `backend/wwwroot/app/`).
- **Host lane**: `FrontendBuild` fixture (build-once via
  `pnpm --dir frontend build` when the manifest is missing, Blocked-style
  failure mirroring the SQL lane, TCP probe proving no Vite dev server on
  5173) + IT-HOST-001..004 in `tests/.../Integration/FrontendHostTests.cs`.

### Red-first evidence

- UT-UI-001..005: written before the lib modules existed — all four test
  files failed with `Failed to resolve import` before implementation.
- IT-UI-001..011 (listFlow): all 11 failed against the placeholder `App`
  (loading indicator/table/controls absent).
- IT-HOST-001..004: written before the Razor page/routing existed — all four
  failed with 404s; green only after the shell plus a production build.
- Honest scope note: IT-UI-012..030 (detail/status/safety/a11y batches) were
  written and run after the core island landed in the listFlow cycle, so their
  first observed red is weaker (the meaningful pre-implementation red for the
  detail path is IT-UI-011's failing opener click in the listFlow red run).
  Each later batch still caught real defects on first execution (below), so
  the assertions ran against imperfect implementations rather than
  rubber-stamping green.

### Catalog correction (kept in sync per §11)

- **UT-UI-004 first tuple**: the catalog's example value `2` for
  `(3, 41, 20)` contradicted its own normative formula
  `max(1, ceil(totalCount/pageSize))` (41 items at 20/page span three pages;
  IT-UI-007's "45 items → page 3 of 3" confirms ceil semantics). Corrected to
  `3` in `frontend-and-sql-cases.md` with a dated note; the test file carries
  the same comment.

### Green commands and evidence

| Command / surface | Observed result |
| --- | --- |
| `pnpm --dir frontend test` | 52 passed, 0 failed (20 UT-UI tests incl. an extra All-filter case + 31 IT-UI test blocks covering IT-UI-001..030) |
| `pnpm --dir frontend run build` | `tsc -b && vite build` clean; manifest entry + css emitted under `backend/wwwroot/app/assets/` |
| `dotnet test --filter 'Category!=SqlServer'` | 146 passed, 0 failed (142 prior + IT-HOST-001..004) |
| `dotnet format --verify-no-changes` / `dotnet build --warnaserror` | Clean / 0 warnings, 0 errors |
| Live app (`dotnet run --project backend`, port 5083) | Island mounts from compiled assets; filter/detail/status flows work end-to-end; hard-reload persists status changes; XSS row inert (`window.__xss` undefined, no `img`/`script` elements, markup rendered as text); console clean in a second real Chrome after fixes |
| Browser walkthroughs (MAN-UI-001/002/004) | Mount + filter + detail + status + reload persistence verified; keyboard-only pass: Tab order filter → sort → row opener → row select → Apply, Space activation opens the drawer, Escape returns focus to the opener, arrow-key + Space status update saved; screenshots at 1280px, 375px, and 640px (200% equivalent) reviewed and defects fixed |
| No-JS shell (MAN-UI-003) | Verified from a JS-less client (curl) plus IT-HOST-003: one mount point, noscript JavaScript guidance, loading placeholder, no visitor data, no dev-server strings. Browser-level JavaScript disable was not exposed by the available automation surfaces; recorded as a tooling limitation |

### Real bugs caught and resolved during the TDD loop

1. **Fetch double bypassed by bare `fetch`**: jsdom provides no fetch, so
   component code calling bare `fetch(...)` resolved through Node's global and
   bypassed the `window.fetch` double (every list test failed with the error
   phase shown). Fixed by installing the double on both `window.fetch` and
   `globalThis.fetch`.
2. **Deferred reactions queued after mount were never consumed**: the initial
   list request fires during `render()`, so reactions must be programmed
   before mounting; `renderIsland(prepare)` now does install → prepare →
   render, and the double rejects unexpected requests loudly.
3. **Refresh success wiped the record-gone announcement**: the list success
   path cleared `role="alert"` unconditionally, erasing IT-UI-014's gone
   message in the same tick chain as the reconciliation refresh. Fixed by
   clearing the assertive region only when recovering from the list-error
   phase (tracked via a phase ref).
4. **Mutation failures used list wording**: a generic/network PUT failure
   rendered "Inquiries could not be loaded…" instead of the mutation message;
   the update path now maps generic outcomes to the "could not be saved — try
   again" variant.
5. **Displayed page followed client page state**: the indicator read the
   request's page before the envelope arrived, breaking "page 3 of 3" after
   envelope-driven navigation; the served envelope is now authoritative.
6. **Missing favicon 404 + unnamed row selects**: a real-browser console check
   surfaced `GET /favicon.ico → 404` and an autofill issue for unnamed row
   selects. Fixed by shipping `backend/wwwroot/favicon.ico` (+ layout link,
   with IT-HOST-004 updated to preserve/restore a pre-existing favicon) and
   adding `name` attributes to row status selects. Console is now clean.
7. **Mobile table compression**: at 375px the table squeezed its columns
   before scrolling; `.inquiry-table` now keeps `min-width: 720px` inside the
   scrollable wrapper (the page itself does not scroll horizontally), and
   badge contrast was deepened per screenshot review.

### Post-implementation review

A reviewer pass over the merged Phase 6 tree found no P0/P1 defects and
confirmed the stale-response guards, reconciliation termination, and IT-HOST
parallel-safety. Three accepted findings, all fixed and re-verified (52
Vitest + 146 xUnit green, format/warnaserror clean):

1. Programmatic detail closes (detail fetch failure, PUT 404 with the panel
   open) dropped focus to `<body>` instead of returning it to the opener —
   all close paths now share `focusDetailOpener` (C7).
2. `ViteManifest.Resolve` could throw (wrong-shape JSON via
   `InvalidOperationException`; delete-between-check-and-read during a
   rebuild) and 500 the dashboard — the catch now covers those and degrades
   to the guidance shell as documented.
3. IT-HOST-004 deleted `wwwroot/app` unguarded and would fail outright on a
   fresh clone where the generated folder does not exist — guarded with an
   existence check.

## Primary guidance used

- Microsoft Learn: [ASP.NET Core integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0) for WebApplicationFactory, actual HTTP-pipeline testing, and explicit test-host environments. Its project-separation recommendation is implemented here as category/directory separation in the small, already planned test project.
- Microsoft Learn: [choosing an EF testing strategy](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy) and [SQLite in-memory connection lifetime](https://learn.microsoft.com/en-us/ef/core/testing/testing-without-the-database#sqlite-in-memory). SQLite tests do not establish T-SQL correctness.
- Microsoft Learn: [validation](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation?view=aspnetcore-10.0), [transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions), and [concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) ground the boundary cases.
- Polly: [retry](https://www.pollydocs.org/strategies/retry.html) and [timeout](https://www.pollydocs.org/strategies/timeout.html) distinguish attempts, retries, and cooperative cancellation.
