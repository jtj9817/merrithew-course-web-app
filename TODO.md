# TODO — Course Inquiry Dashboard

Implementation checklist for the target design. **All phases 0–9 are
implemented and verified**; the loop is closed — see the
[Phase 9 implementation record](docs/testing/tdd-plan.md#phase-9-implementation-record).
**ADR** = decision record in `docs/architecture/adr/`;
**REQ/VER** = requirement / verification in `docs/architecture/model.json`.
The load-bearing rule is **persist first, sync second**.

Order is roughly dependency-first. Bonuses are folded in where they belong, since
all four were committed (Swagger, CRM retry, structured logging, pagination).

**Test-first extension:** follow [`docs/testing/tdd-plan.md`](docs/testing/tdd-plan.md)
and the [backend](docs/testing/backend-cases.md) /
[frontend, hosting, and SQL](docs/testing/frontend-and-sql-cases.md) case catalogs.
For every behavior below: select cases → observe a meaningful failing assertion →
implement → pass → refactor → exercise the actual surface. Tests are not deferred
to Phase 7 or optional “as time allows.” The extension's vertical-slice order takes
precedence when adjacent phases need to be interleaved.

[Boundary contracts C1–C8](docs/architecture/contracts.md) settle input limits,
status omission, no-op updates, paging, cancellation, retry budgets, and SQL
query semantics. All verification statuses are **passing** with recorded
evidence in the model (flipped 2026-09-17 after the Phase 9 full-suite re-run).

---

## Phase 0 — Solution scaffolding

- [x] Create the .NET 10 solution and Web API project under `backend/` (`dotnet new webapi --use-controllers`, not minimal API) — ADR-0002
- [x] Add `tests/CourseInquiryDashboard.Tests` with xUnit, backend reference, Unit/Integration/SqlServer groups, `Category` and `CaseId` traits — ADR-0002, 0009
- [x] Add a solution file wiring `backend/` + `tests/`
- [x] Add compatible .NET 10 packages: EF Core + SQLite, Polly, Swagger/OpenAPI, `Microsoft.AspNetCore.Mvc.Testing`, xUnit runner/Test SDK, controlled-time testing support, and SqlClient for the separate SQL test lane — ADR-0003, 0007, 0009
- [x] Establish isolated SQLite/host fixtures, synthetic inputs, controlled clock/CRM, and full structured-log capture; do not use mocked DbSet or EF InMemory for query evidence
- [x] Confirm build and test discovery work; observe the first catalog test's intended red assertion (an empty suite is not feature evidence)

## Phase 1 — Domain & persistence — ADR-0003

- [x] `Status` enum: New, Contacted, Pending, Registered, Closed (New = default) — REQ-APP-001
- [x] `CourseInquiry` entity: int identity Id, First/Last/Email/Phone/CourseName/PreferredLocation/Message, Status, CreatedDate, UpdatedDate — see `docs/domain/course-inquiry.md`
- [x] `AppDbContext` mapping: required/null columns, C1 lengths, named status conversion/constraint, UTC read semantics; email is not unique — C8
- [x] Initial EF migration applied at startup; verify empty-file migration, repeat startup/data survival, and fatal migration failure — VER-DATA-003
- [x] `database/database.sql` (SQL Server dialect + SQLite note): matching DDL, ≥5 synthetic samples, and the C8 last-seven-days, count-by-status, normalized-duplicate-email queries — VER-DATA-002
- [x] Write then pass VER-DATA-001 persistence/UTC round-trip cases using fresh contexts; execute SQL-script cases separately on SQL Server, not SQLite

## Phase 2 — DTOs & validation — ADR-0005

- [x] `CreateInquiryDto`: explicit required fields, email validation, C1 length boundaries/optional preservation; ignore unknown/server-owned JSON fields — REQ-API-002
- [x] `UpdateStatusDto`: required nullable status, explicit name-only conversion and membership validation; reject missing/null/numeric/composite values — REQ-APP-002, C2
- [x] `InquiryResponse` and page-envelope DTOs; camelCase JSON, canonical status names, nullable fields, UTC timestamps; don't expose the entity — C1, C4
- [x] Write unit validation cases before DTO behavior; prove representative binding/serialization/400 ProblemDetails through HTTP integration — VER-API-002, VER-APP-002

Evidence and scope: [implementation record](docs/testing/tdd-plan.md#phases-02-implementation-record).
HTTP validation uses test-only controllers through the real MVC host; it does not
claim the Phase 5 inquiry routes or the Phase 3 service guard are implemented.
Aggregate model verifications remain planned pending all mapped later-phase cases.

## Phase 3 — Application/service layer — ADR-0005

- [x] `IInquiryService` + `InquiryService`
- [x] Create: force New, sample injected UTC once for both timestamps, await one committed insert, then await best-effort CRM — REQ-APP-001, REQ-CRM-001, C5
- [x] List: filter before count/page, deterministic CreatedDate+Id sorting, C4 bounds and page envelope; count/page are separate queries — REQ-API-004
- [x] GetById; UpdateStatus (same-status no-op, actual-change timestamp); hard delete and zero-row race handling — ADR-0006, C2–C4
- [x] Write then pass defaults/no-op/frozen-clock/backward-clock cases — VER-APP-001
- [x] Write then pass free-form/invalid-status and permanent-delete/retained-Closed cases — VER-APP-002, VER-APP-003
- [x] Prove independent-connection commit visibility before CRM, definite write failure without CRM, pre/post-commit cancellation, and duplicate POST behavior — VER-SYS-002, VER-CRM-001

## Phase 4 — CRM integration — ADR-0007

- [x] `ICrmClient` Task-returning port + deterministic `SimulatedCrmClient`; completion = success, exceptions = failure; no endpoint/keys or visitor-triggered failure modes — REQ-CRM-001
- [x] Real Polly pipeline per C6: transient-only exponential retry, maximum four attempts, inner attempt and outer total timeouts, cooperative cancellation — REQ-CRM-002, REQ-CRM-004
- [x] Structured attempt/final-outcome logs via `ILogger`, omitting visitor fields and raw exceptions across scopes, telemetry, middleware, and EF — REQ-CRM-003, REQ-SYS-004
- [x] Await sync AFTER commit; isolate CRM failure/cancellation without rolling back or replaying creation; do not swallow database errors — REQ-SYS-003
- [x] Write then pass create-survives-CRM-failure HTTP/SQLite integration — VER-CRM-001, VER-SYS-003
- [x] Write then pass real-pipeline retry/exhaustion/permanent/timeout/cancel and sink privacy cases with controlled time — VER-CRM-002, VER-CRM-003, VER-CRM-004, VER-SYS-004

## Phase 5 — API layer — ADR-0005

- [x] `InquiriesController`: `POST /api/inquiries`, `GET /api/inquiries` (status filter + paging + sort), `GET /api/inquiries/{id}`, `PUT /api/inquiries/{id}/status`, `DELETE /api/inquiries/{id}` — REQ-API-001
- [x] Consistent ProblemDetails for validation/binding/routing/resource/media-type errors and sanitized exceptions in Development/Production — REQ-API-003, REQ-SYS-005, C3
- [x] OpenAPI/Swagger documents actual request/response shapes and errors for all five endpoints; executable manual create/update surface — REQ-API-005
- [x] Write then pass happy-path, validation precedence, 404/repeat-delete, checked paging, create-then-read/list, and safe-error HTTP cases — VER-API-001 through VER-API-005, VER-SYS-002, VER-SYS-005

Evidence and scope: [Phases 3–5 implementation record](docs/testing/tdd-plan.md#phases-35-implementation-record).
All 51 mapped catalog cases (IT-APP-001..020, UT-CRM-001..010, IT-API-001..021) pass.
Aggregate model verifications with frontend or browser components remain planned
pending Phases 6 and 9.
## Phase 6 — Frontend — ADR-0004

- [x] Vite + React + TypeScript in `frontend/`; Vitest/jsdom/Testing Library + user-event test harness with non-watch `test` script — ADR-0004, 0009
- [x] Production assets in a dedicated `backend/wwwroot` subfolder; Razor `/dashboard` resolves the manifest entry/CSS/imports, renders one mount point and loading/no-JavaScript guidance
- [x] Write then pass component integration for list/filter/paging/detail/status and clear success/error feedback — REQ-UI-001
- [x] Write then pass stale-response/unmount handling, pending mutations, filter/page reconciliation, missing records, safe text/errors, keyboard/focus/live regions — REQ-UI-002, C7
- [x] Keep create/delete in API/Swagger; the visitor form and island create/delete controls are out of scope (resolves `ann.create-ui`)
- [x] Verify Razor and real compiled assets through host integration, then run browser triage/accessibility checks — VER-UI-001, VER-UI-002, VER-SYS-001

Evidence and scope: [Phase 6 implementation record](docs/testing/tdd-plan.md#phase-6-implementation-record).
UT-UI-001..005, IT-UI-001..030 (52 Vitest tests) and IT-HOST-001..004 (146
non-SQLServer .NET tests total) pass; MAN-UI-001/002/004 browser walkthroughs
ran against the live app (screenshots at 1280/375/640px, keyboard pass, clean
console). MAN-UI-003's browser-level JavaScript disable was not exposed by the
automation surfaces — evidenced instead by a JS-less client fetch plus
IT-HOST-003's automated shell assertions. One catalog example corrected in
sync (UT-UI-004 tuple, see the record). Aggregate VER-* statuses in model.json
remain planned until Phase 9.

## Phase 7 — Regression gate (tests were written in each slice)

- [x] Confirm every catalog case has executable red/green evidence; both defaults/timestamps and CRM-failure isolation are required, not alternative minimum candidates
- [x] Build frontend assets, then pass all local .NET unit/SQLite/host integration and frontend unit/component integration tests; zero discovered tests are not success
- [x] Pass the SQL Server category on a disposable database; absent infrastructure blocks VER-DATA-002 rather than silently skipping it
- [x] Run the documented browser/Swagger checks; retain manual evidence separately from automated results

Evidence and scope: [Phase 7 implementation record](docs/testing/tdd-plan.md#phase-7-implementation-record)
and [manual evidence](docs/testing/manual-evidence.md). All 80 xUnit + 35
Vitest catalog cases map to executable tests and pass (146 non-SQLServer, 8
SqlServer on a fresh disposable container, 52 Vitest); VER-APP-001 and
VER-CRM-001 are covered as primary mapped evidence, not alternative minimums.
Red evidence is not per-case uniform: the weak/disclaimed batches recorded in
earlier phase records (IT-UI-012..030; Phases 0–2 permutations) still stand.
MAN-UI-003 keeps its documented tooling substitution (JS-less fetch +
IT-HOST-003). Browser walkthrough and screenshot review ran through delegated
agents despite the Agent tool rejecting `task`/`vision-agent` dispatches
(provider `reasoning-level-missing`) — see the record's scope notes.

## Phase 8 — Deliverable docs

- [x] `README.md`: setup + run instructions (incl. .NET 10 SDK requirement, frontend build step), assumptions, **hard-delete rationale** (ADR-0006), what I'd improve with more time (soft-delete/audit, durable CRM outbox, auth), AI-tools disclosure
- [x] `written-answers.md`: Troubleshooting, Security, Accessibility, Code quality — draw Security/Code-quality from the ADRs, Troubleshooting from `flow--flow-submit.html` + the SQL queries
- [x] Fill `docs/backend/` and `docs/frontend/` alongside the code (deferred in planning); seed `docs/CHANGELOG.md`

Evidence and scope: `README.md` and `written-answers.md` were rewritten from the
Phase 0–7 code (both previously described only the Phase 0–2 state); every claim
traces to a source file, ADR, or contract. New docs: `docs/backend/README.md`,
`docs/frontend/README.md`, and a seeded `docs/CHANGELOG.md` — they cross-reference
the ADRs/contracts rather than duplicating them. `docs/issues/` was intentionally
skipped (troubleshooting lives in `written-answers.md` per the brief; no runbook
warranted at this scope). Test counts cited in the docs (146 non-SqlServer + 52
Vitest + 8 SqlServer) are the recorded Phase 7 results, not re-run here — a fresh
full-suite pass and the model `planned → passing` update remain Phase 9 work.

## Phase 9 — Verify & close the loop

- [x] Run the built app without a Vite dev server; exercise Swagger and triage, keyboard/focus, safe feedback, and sanitized logs using synthetic data
- [x] Update model verifications from `planned` to `passing` only after all mapped cases/supplemental checks run; record test paths, command/result/revision, then regenerate diagrams
- [x] Verify setup from a clean checkout, complete written answers, and review every item of the spec's Submission Requirements checklist; report blocked evidence explicitly

Evidence and scope: [Phase 9 implementation record](docs/testing/tdd-plan.md#phase-9-implementation-record)
and [manual evidence](docs/testing/manual-evidence.md#phase-9--built-app-walkthrough-no-vite-dev-server).
Full three-lane re-run at `5d5fbe0`: 146 non-SqlServer xUnit + 52 Vitest + 8
SqlServer all passing; built-app walkthrough (compiled `/app/assets/*`, no dev
server) all PASS including "Status saved." feedback, keyboard triage, XSS
inertness, Swagger manual create/update, and a zero-hit PII sentinel log scan;
all 22 model verifications flipped to `passing` with per-entry evidence paths
and revision; diagrams regenerated (traceability now reads 22/22). Clean
checkout verified against README verbatim; its two findings were fixed in the
docs (README port-override note incl. the `--no-launch-profile`/Development
trap, AGENTS.md `#dashboard-root` mount point). No blocked evidence; tooling
substitutions (Space-on-select, swagger-ui editor fill, viewport emulation)
are recorded in the record's scope notes.

## Phase 10 — Runtime CRM simulation demonstration

Ordered checklist for exposing the simulated CRM outcome at runtime and in the
dashboard UI. The full design, boundaries, and evidence plan live in
[`docs/plans/crm-simulation-plan.md`](docs/plans/crm-simulation-plan.md).

- [x] `CrmSimulationRuntime` + `CrmSimulationOptions`: configuration-bound startup defaults, thread-safe per-sync settings snapshots, and the six deterministic attempt modes (`Success`, `TransientThenSuccess`, `AlwaysTransientFailure`, `PermanentFailure`, `Timeout`, `InternalCancellation`) — ADR-0007, C6
- [x] `SimulatedCrmClient` production constructor executing the configured runtime mode through the existing Polly pipeline; the simulated external boundary now receives an explicit `CrmInquiryPayload` — REQ-CRM-001
- [x] `InquiryService` isolates CRM-originated cancellation (not just caller cancellation), so no post-commit CRM outcome escapes `CreateAsync` — REQ-SYS-003, C5
- [x] `/api/dev/crm-simulation` opt-in endpoints (`DevTools:CrmSimulation` or Development): mode catalog, runtime selection, and safe non-PII sync results — C6
- [x] `appsettings.json` documents the `CrmSimulation` section with validated ranges (`ValidateDataAnnotations` + `ValidateOnStart`)
- [x] Write then pass `CrmPipelineTests` runtime-mode cases: each mode's attempt count, retry classification, and privacy-clean logs (`CrmRuntimeSimulationTests`, UT-CRM-011..017 + snapshot test) — VER-CRM-002, VER-CRM-003
- [x] Write then pass integration cases for the dev endpoints: catalog shape, validation 400s, unknown-mode 400, unknown-result 404, result readback (`CrmSimulationEndpointTests`, IT-CRM-SIM-001..008) — C3, C6
- [x] Add an integration case proving `InternalCancellation` returns `201` with the row intact (previously an escaping-cancellation gap) — IT-CRM-SIM-007 — VER-CRM-001, VER-SYS-003
- [x] Frontend dev tool client (`lib/crmSimulation.ts`): runtime-parsed types with runtime shape validation, no unchecked casts — ADR-0004
- [x] `CrmSimulationControl` island component: mode/latency/failure-count controls, Apply, and Run demo inquiry through the real `POST /api/inquiries` — VER-UI-001
- [x] Wire `window.__crmSimulationTools` through `Dashboard.cshtml` so the control renders only when the endpoints are mapped (same pattern as scenario seeding) — C7
- [x] Component tests for the control: catalog load, apply failure wording, run-demo success/failure/unavailable-result paths (`crmSimulationControl.test.tsx`) — REQ-UI-002
- [x] Browser walkthrough of all six modes incl. queue refresh, safe logs, and the persist-first invariant visible in the UI (every demo row persisted; log trail matched the pipeline; zero-hit PII scan) — VER-UI-002, VER-SYS-001
- [x] Docs: `docs/backend/README.md` (runtime modes + endpoints), `docs/frontend/README.md` (control), ADR-0007 addendum, and `docs/CHANGELOG.md` entry

Evidence and scope: complete. Runtime + cancellation isolation at `74eafc9`
(smoke-proven: `TransientThenSuccess` = 3 attempts/success, `PermanentFailure`
= 1 attempt/failed/row intact, `InternalCancellation` = 1 attempt/cancelled/row
intact). Automated coverage at `16478d2` — 16 CRM-simulation tests; full non-
SqlServer suite 162/162 passing (the SqlServer lane still requires container
infrastructure, unchanged). Island control + component tests at `cb3cbf4` (58
Vitest passing); six-mode browser walkthrough on the built app confirmed each
outcome, attempt count, queue refresh, and a zero-hit PII log scan.

---

**Traceability:** every REQ/VER above is in the model; exact case mappings are in
the catalogs and [TDD evidence index](docs/testing/tdd-plan.md#requirement-to-evidence-index).
Planned coverage in `docs/architecture/traceability.html` is not a passing result.
