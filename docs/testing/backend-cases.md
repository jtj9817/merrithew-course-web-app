# Backend test-case catalog — DTO, service, HTTP, SQLite, CRM, logging

> **Implemented and verified specification.** Every backend case in this catalog
> has executable evidence; the aggregate Phase 9 run is recorded in the
> [TDD implementation record](tdd-plan.md#phase-9-implementation-record).
> This catalog verifies boundary contracts
> [C1–C8](../architecture/contracts.md) recorded in
> [ADR-0009](../architecture/adr/0009-testable-boundary-contracts.md).
> REQ/VER identifiers and passing evidence live in the architecture
> [model](../architecture/model.json). Frontend cases (`VER-UI-001/002`), SQL
> Server script cases (`VER-DATA-002`), and supplemental browser walkthroughs
> remain owned by the sibling catalog and manual evidence record.

## 1. Levels and conventions

| Prefix | Level | Meaning |
| --- | --- | --- |
| `UT-VAL` | Unit | Pure in-process DTO/DataAnnotations and status-parsing rules. No database, no HTTP. |
| `UT-CRM` | Unit | Real Polly resilience pipeline around a scripted in-process CRM attempt, plus log-sink assertions. No database, no HTTP. |
| `IT-APP` | Integration | `InquiryService` over real SQLite via EF Core with a controlled `TimeProvider`. Per ADR-0009 an EF-backed service test is **integration**, never a unit. No mock `DbSet`, no repository abstraction. |
| `IT-API` | Integration | Full HTTP host (e.g. `WebApplicationFactory`) over real SQLite. Routing, binding, JSON, ProblemDetails, OpenAPI facts are proven **only** here — never by invoking controllers directly. |
| `IT-DATA` | Integration | Migrations, restart durability, and schema shape against real SQLite files, through the real startup path. |

Conventions applied to every case:

- **Controlled time only.** Timestamps come from an injected `TimeProvider`
  test double (`FIX-TIME`). No `Thread.Sleep`, no wall-clock waits, no
  timing races. Polly backoff delays run for real (the slowest scenario is
  bounded by the 2 s outer budget); tests assert **attempt counts, ordering,
  and outcome categories**, never stopwatch equality.
- **Real failure injection, no fake layers.** Faults enter through real
  mechanisms: scripted CRM outcomes (`FIX-CRM`), EF `SaveChangesInterceptor`
  hooks (`FIX-INTERCEPT`), a second independent SQLite connection for out-of-band
  mutation/probing (`FIX-PROBE`), and process kill for crash cases. Logging is
  captured at a real sink (`FIX-LOG`).
- **Determinism and isolation.** Each test gets a unique fresh database
  (`FIX-DB`); no test depends on execution order or shared state.
- **Do not pin framework wording.** Error `title`/`detail` text, JSON property
  order, and exact problem `type` URLs are never asserted; structure, status
  codes, and `errors` keys are.
- Rows are specifications, not blanket passing claims. See the execution record
  for covered cases; aggregate model verification entries remain `planned`.

### Project and trait mapping (per parent TDD plan)

All .NET cases here live in the single repository test project
`tests/CourseInquiryDashboard.Tests` — this catalog introduces **no** separate
backend test project:

- `UT-VAL-*` and `UT-CRM-*` → `Unit/` directory, `[Trait("Category", "Unit")]`.
- `IT-APP-*`, `IT-API-*`, `IT-DATA-*` → `Integration/` directory,
  `[Trait("Category", "Integration")]`.
- Every case additionally carries its ID as a `CaseId` trait,
  e.g. `[Trait("CaseId", "IT-API-019")]`.
- This catalog defines **no** `SqlServer` category cases; the sibling catalog
  owns `IT-SQL-*` under `SqlServer/`, selected by the separate explicit release
  lane. The default local run `dotnet test --filter 'Category!=SqlServer'`
  therefore executes every case in this document. Frontend tests remain
  `frontend/src/**/*.test.ts(x)` and are unrelated to this catalog.

## 2. Fixtures

| ID | Fixture | Contract |
| --- | --- | --- |
| `FIX-DTO` | Builder returning a synthetic **valid** `CreateInquiryDto` with distinctive sentinel values (e.g. first name `Aiko`, last name `O'Brien`, email `aiko.obrien+test@example.com`, phone `+64 21 555 0123`, course `Patisserie 301 — evening`, location `Auckland CBD`, message containing markup `Welcome to <b>term 2</b>` and a Unicode em dash). Mutator helpers produce each invalid variant. Sentinels double as privacy-scan tokens (`FIX-LOG`). | C1 |
| `FIX-TIME` | Minimal **mutable `TimeProvider` test double** (own settable `GetUtcNow` field — not the packaged `FakeTimeProvider`, whose `SetUtcNow` may reject backward movement) pinned to `2026-03-01T10:00:00Z`; supports forward advance and rollback to an earlier instant for the clock-correction case. Injected into the service/host in every case. | C2 |
| `FIX-DB` | Fresh SQLite database per test: unique temp-file connection (or kept-open `:memory:` connection where a single connection suffices), schema applied by running the **real EF migrations** — never `EnsureCreated`. Temp file is mandatory wherever a second connection must observe committed data. | C8 |
| `FIX-CRM` | `ScriptedCrmClient : ICrmClient`: queue of scripted outcomes (success; transient `HttpRequestException`; hang until per-attempt timeout; `OperationCanceledException`; permanent `InvalidOperationException`; unknown exception), a `TaskCompletionSource` gate to block/park an attempt, call recording (received inquiry fields, call order), and an optional cooperative delay honoring the inner token. Behavior is **configured, never derived from visitor data**. | C5, C6 |
| `FIX-LOG` | Test `ILoggerProvider` capturing every entry at the sink: category, level, **rendered message**, structured state key/value pairs, full scope stack, and the **exception object**. Helper `AssertPrivacy(entries, sentinels)` scans all four surfaces for DTO sentinel values/fragments and asserts state keys ⊆ allow-list (`inquiryId`, `attempt`, `outcome`, `duration`, error category/type). | C6 |
| `FIX-HTTP` | `WebApplicationFactory` wired with `FIX-DB`, singleton `FIX-TIME`, `FIX-CRM`; JSON client (camelCase, `application/json`). `UseEnvironment` switches Development/Production for sanitization cases. | C3 |
| `FIX-INTERCEPT` | Real EF `SaveChangesInterceptor` with a test-controlled hook fired inside `SavingChanges`: cancel the token, delete/drop the target via `FIX-PROBE`, or throw. Deterministic mid-save fault injection without mocking `DbSet`. | C4, C5 |
| `FIX-PROBE` | Second, independent `SqliteConnection` to the same `FIX-DB` temp file for reading committed rows and executing out-of-band SQL while the service runs. | C5, C8 |

## 3. Unit cases — DTO validation and status rules (`UT-VAL`)

Exercised via `System.ComponentModel.DataAnnotations.Validator` (or the
equivalent service-boundary parser) against `FIX-DTO` variants. Full
permutation coverage lives here; HTTP cases reuse only representative bodies.

| Case ID | Arrange / input | Act | Observable result | Traceability |
| --- | --- | --- | --- | --- |
| UT-VAL-001 | `FIX-DTO` valid baseline (all seven fields) | Validate `CreateInquiryDto` | Zero validation errors; every property binds as sent | REQ-API-002 · VER-API-002 · C1 |
| UT-VAL-002 | For each of `firstName`, `lastName`, `email`, `courseName`: property absent (default `null`) | Validate | Exactly that field flagged required; no other field flagged | REQ-API-002 · VER-API-002 · C1 |
| UT-VAL-003 | Required field set to `""` (empty) and separately to `"   "` (whitespace only), per field of UT-VAL-002 | Validate | Each variant fails with the field flagged; whitespace-only is **not** accepted as present | REQ-API-002 · VER-API-002 · C1 |
| UT-VAL-004 | Optional fields (`phone`, `preferredLocation`, `message`): absent/null; separately `""` and `"  x  "` | Validate and inspect bound values | Absent/null validate with value `null`; empty and whitespace variants **validate and are preserved byte-for-byte** — no trim, no case fold, no encode | REQ-API-002 · VER-API-002 · C1 |
| UT-VAL-005 | Per field, value at exactly the C1 maximum in UTF-16 code units (100/100/254/50/200/200/4000), using ASCII for one variant and a string of surrogate pairs (e.g. 50 surrogate chars = 100 units for `firstName`) for another | Validate | Both variants pass — the limit counts UTF-16 code units, not characters | REQ-API-002 · VER-API-002 · C1 |
| UT-VAL-006 | Per field, maximum + 1 UTF-16 code unit (repeat the two encodings of UT-VAL-005) | Validate | Each variant fails with `StringLength` error naming the field; nothing truncated silently | REQ-API-002 · VER-API-002 · C1 |
| UT-VAL-007 | Email variants: `a.b+c@sub.domain.co`, uppercase local part, numeric-only local part, minimal `a@b.cc`, `a@b`, `space s@x.com` | Validate `email` | All pass — .NET 10 `[EmailAddress]` accepts a single non-edge `@`; it does not require a dotted domain or reject internal spaces. No deliverability or stricter RFC rule is added. | REQ-API-002 · VER-API-002 · C1 |
| UT-VAL-008 | Email variants: `no-at-symbol`, `two@@ats.com`, `@leading.com`, `trailing@` | Validate `email` | Each fails with `email` flagged; error does not echo other fields | REQ-API-002 · VER-API-002 · C1 |
| UT-VAL-009 | Status parser input: each of the five canonical names, each lower/UPPER/mixed-cased, each wrapped in surrounding whitespace (`"  pending "`) | Parse (update-body rule and optional list filter — same parser) | Parses to the defined enum member; canonical output name has contract casing (`Pending`); input casing/whitespace never stored | REQ-APP-002 · VER-APP-002 · C2 |
| UT-VAL-010 | Status parser inputs: unknown word `Draft`, `""`, `"New,Closed"` (comma list), `0`, `1`, `"0"`, `"1"`, `true`, `[..]` array, `{..}` object | Parse | Every variant is rejected with an invalid-status result — **never** coerced, never defaulted to `New` | REQ-APP-002 · VER-APP-002 · C2 |
| UT-VAL-011 | `UpdateStatusDto` with `status` absent and separately `null` | Validate | Validation error on `status` (required nullable at the DTO boundary); result is **invalid**, not `New` — a non-nullable enum's silent zero-default must be impossible here | REQ-APP-002 · VER-APP-002 · C2 |
| UT-VAL-012 | Defined-enum guard input: `(InquiryStatus)99` (out-of-range cast, HTTP bypassed) | Run service-boundary guard | Guard rejects before any persistence; no `NullReferenceException`, no default behavior | REQ-APP-002 · VER-APP-002 · C2 |

## 4. Unit cases — CRM pipeline and privacy (`UT-CRM`)

Real Polly pipeline (total timeout **2 s** → retry → per-attempt timeout
500 ms → simulated operation; delays 100/200/400 ms, no jitter) around
`FIX-CRM`; logging asserted through `FIX-LOG`. Attempt counts and order are
exact; the schedule below makes the outer budget deterministically reachable
with production settings.

| Case ID | Arrange / input | Act | Observable result | Traceability |
| --- | --- | --- | --- | --- |
| UT-CRM-001 | `FIX-CRM` default success; inquiry id 7 | Run pipeline | Exactly 1 attempt; outcome `success` logged with `inquiryId=7`; attempt-start and terminal-outcome entries present | REQ-CRM-002 · VER-CRM-002 · C6 |
| UT-CRM-002 | `FIX-CRM`: transient `HttpRequestException` ×2, then success | Run pipeline | Exactly 3 attempts in order; final outcome `success`; each attempt's start/outcome logged with its attempt number | REQ-CRM-002 · VER-CRM-002 · C6 |
| UT-CRM-003 | `FIX-CRM`: fast transient `HttpRequestException` on every attempt | Run pipeline | Exactly **4 attempts** (original + 3 retries), starting at ≈0/100/300/700 ms — backoff delays observed in order (100→200→400 ms; ordering asserted, not stopwatch precision); pipeline **rethrows at exhaustion** to the service's post-commit isolation boundary; final outcome `failed` logged | REQ-CRM-002 · VER-CRM-002 · C6 |
| UT-CRM-004 | `FIX-CRM`: attempt 1 hangs past 500 ms, attempt 2 succeeds | Run pipeline | Attempt 1 ends in `TimeoutRejectedException` (cooperative per-attempt timeout), is **retried**; 2 attempts; final outcome `success` | REQ-CRM-002 · VER-CRM-004 · C6 |
| UT-CRM-005 | `FIX-CRM`: every attempt hangs past 500 ms | Run pipeline | Deterministic with production settings: attempts start at ≈0/600/1300 ms and each ends in `TimeoutRejectedException`; the **outer 2 s total expires during the 400 ms backoff**, so the fourth attempt never starts; exactly 3 attempts; terminal outcome `timedOut` logged; the outer timeout is not retried; exception propagates to the isolation boundary | REQ-CRM-002 · VER-CRM-002 · REQ-CRM-004 · VER-CRM-004 · C6 |
| UT-CRM-006 | `FIX-CRM`: throws `OperationCanceledException` on attempt 1 | Run pipeline | Exactly 1 attempt — cancellation is **never retried**; outcome `cancelled` logged; exception propagates to the post-commit catch | REQ-CRM-004 · VER-CRM-004 · C6 |
| UT-CRM-007 | `FIX-CRM`: attempt 1 fails transiently; cancel the inner token during the first backoff delay | Run pipeline | No second attempt starts; outcome `cancelled`; cancellation during backoff cannot trigger a new attempt | REQ-CRM-004 · VER-CRM-004 · C6 |
| UT-CRM-008 | `FIX-CRM`: permanent simulated rejection (`InvalidOperationException`) | Run pipeline | Exactly 1 attempt — permanent failures are not retried; outcome `failed` (category recorded, message not); exception propagates | REQ-CRM-002 · VER-CRM-002 · C6 |
| UT-CRM-009 | `FIX-CRM`: unknown exception type (neither transient nor permanent allow-list) | Run pipeline | Exactly 1 attempt — the retry allow-list (transient `HttpRequestException`, per-attempt `TimeoutRejectedException`) is exact | REQ-CRM-002 · VER-CRM-002 · C6 |
| UT-CRM-010 | Grouped row — same privacy invariant exercised across the five terminal scenarios: success, retry, exhaustion (UT-CRM-003), timeout (UT-CRM-005), cancellation (UT-CRM-006) | Run each; scan complete sink via `FIX-LOG.AssertPrivacy` | No sentinel/fragment of any visitor field (name, email, phone, message, location, course) appears in **rendered messages, structured state, scopes, or exception objects**; state keys ⊆ allow-list; raw exception objects/messages/inner exceptions are never attached; Polly telemetry middleware output obeys the same scan | REQ-CRM-003 · VER-CRM-003 · REQ-SYS-004 · VER-SYS-004 · C6 |

## 5. Integration cases — service over SQLite (`IT-APP`)

`InquiryService` + real EF Core/SQLite (`FIX-DB`) + `FIX-TIME` + `FIX-CRM`.
These are integration tests; none is labelled unit.

| Case ID | Arrange / input | Act | Observable result | Traceability |
| --- | --- | --- | --- | --- |
| IT-APP-001 | `FIX-DTO` with Unicode, apostrophes, markup, leading/trailing whitespace in optional fields | `CreateAsync` | Persisted row has server-owned `id` (positive), `status=New` forced, `createdDate == updatedDate == T0` (UTC read **once**), and every visitor field verbatim — no trim/lowercase/truncate/encode | REQ-APP-001 · VER-APP-001 · C1, C2 |
| IT-APP-002 | Two identical submissions (same email; also same full payload), and separately a repeat after a "lost response" | Create twice | Second row created with distinct id — no dedup, no uniqueness, no idempotency | REQ-DATA-001 · VER-DATA-001 · C1 |
| IT-APP-003 | Row at T0; `FIX-TIME` advance 5 min; updates `New→Closed`, `Closed→New` (backward), `Pending→Registered` (skip) | `UpdateStatusAsync` each | Every transition accepted in any direction; `updatedDate == T0+5min` from provider; `createdDate` never changes | REQ-APP-001 · VER-APP-001 · REQ-APP-002 · VER-APP-002 · C2 |
| IT-APP-004 | Row at T0; advance clock; update to the **current** status | `UpdateStatusAsync` | No-op: existing inquiry returned, `updatedDate` unchanged (still T0), no error | REQ-APP-001 · VER-APP-001 · C2 |
| IT-APP-005 | Frozen clock — never advanced | Update status to a different value | Succeeds; `updatedDate == createdDate` (equal instants are legal wall-clock observations, not versions); no error, no conflict | REQ-APP-001 · VER-APP-001 · C2 |
| IT-APP-006 | Row updated at T0+5 min; `FIX-TIME.SetUtcNow` back to T0; update again | `UpdateStatusAsync` | Change stored with the **earlier** `updatedDate` (clock correction); succeeds — timestamps are not conflict-detection versions; no 409/exception | REQ-APP-001 · VER-APP-001 · C2 |
| IT-APP-007 | Row created at T0 | Read via a **fresh** context (`FIX-DB` new context, same file) | All eleven fields round-trip; `DateTime.Kind == Utc` restored where the provider representation drops it; instant exactly equal to T0 | REQ-DATA-001 · VER-DATA-001 · C8 |
| IT-APP-008 | Seed rows across all five statuses; several rows share one `createdDate` via `FIX-TIME` | List with `status=Pending`, then unfiltered; both sort directions | Status filter applied **before** count and paging: `totalCount` counts filtered rows only; order is `CreatedDate` then `Id`, both in the requested direction (ties deterministic); no full in-memory materialization | REQ-API-004 · VER-API-004 · REQ-SYS-001 · C4 |
| IT-APP-009 | Seeded rows; request page 2 with small `pageSize`; then a page beyond the end | List | Page 2 returns the exact expected slice; beyond-end page returns empty `items` with the filtered `totalCount`; counting and page fetch are separate database operations | REQ-API-004 · VER-API-004 · C4 |
| IT-APP-010 | One seeded row | `DeleteAsync` it, then `DeleteAsync` again | First delete removes the row permanently (hard delete — no `Closed` archive row, no flag); second delete reports not-found (zero rows affected) | REQ-APP-003 · VER-APP-003 · C2, C4 |
| IT-APP-011 | Two stale readers of one row; both update status sequentially (second based on the stale first read) | Apply both writes | **Last committed write wins**; second write succeeds; no conflict exception, no 409, no audit requirement | REQ-SYS-001 · C4 |
| IT-APP-012 | `FIX-INTERCEPT` deletes the target row via `FIX-PROBE` inside `SavingChanges`, after EF's update/delete command is prepared | Update, and separately delete, the vanished row | Zero-rows-affected concurrency failure surfaces; service maps it to **not-found** — never recreates the row, never reports write success | REQ-API-003 · VER-API-003 · C4 |
| IT-APP-013 | HTTP bypassed: service called directly with the out-of-range enum from UT-VAL-012 | Create / update | Guard rejects; **nothing persists** — invalid internal enum values cannot enter the store even without HTTP | REQ-APP-002 · VER-APP-002 · C2 |
| IT-APP-014 | `FIX-INTERCEPT` drops the table via `FIX-PROBE` inside `SavingChanges` (definite database failure before commit); `FIX-CRM` recording armed | `CreateAsync` | Create throws the database failure (sanitized to HTTP at the API layer — IT-API-019); `FIX-CRM` recorded **zero** calls; no partial row exists | REQ-SYS-002 · VER-SYS-002 · REQ-CRM-001 · VER-CRM-001 · C5 |
| IT-APP-015 | `FIX-CRM` attempt parked on its gate; `FIX-DB` **temp file** so `FIX-PROBE` can read concurrently | Start `CreateAsync` as a task; poll `FIX-PROBE` until the row is visible; then inspect the task; then release the gate | Row (with committed values) is readable through the **independent connection before the CRM attempt starts**; `CreateAsync` has **not completed** while the gate is parked (CRM is awaited in-request, not fire-and-forget); after release, the task completes successfully and `FIX-CRM` shows exactly one call | REQ-SYS-002 · VER-SYS-002 · C5 |
| IT-APP-016 | `FIX-CRM` exhausts retries (transient ×4) after a successful commit | `CreateAsync`; catch nothing | Create returns its created outcome — **no CRM exception escapes**; row readable from a fresh context with committed values; final outcome `failed` logged | REQ-CRM-001 · VER-CRM-001 · REQ-SYS-003 · C5 |
| IT-APP-017 | Commit succeeds; inner token cancelled while the parked `FIX-CRM` attempt is pending | `CreateAsync` | CRM work stops cooperatively, outcome `cancelled` logged; row retained and readable; created outcome returned; insert is never rolled back or replayed | REQ-CRM-004 · VER-CRM-004 · C5 |
| IT-APP-018 | Token cancelled **before** the write starts (post-validation) | `CreateAsync` | Cancellation propagates — no fabricated success; `FIX-PROBE` shows no row; `FIX-CRM` shows zero attempts | REQ-CRM-004 · VER-CRM-004 · C5 |
| IT-APP-019 | `FIX-INTERCEPT` cancels the token **inside** `SavingChanges` (mid-write) | `CreateAsync` | Only weak invariants asserted, per C5: outcome is never a claimed success without a committed row; if the insert committed, the row is intact and readable and CRM is attempted/skipped cooperatively; ambiguous-outcome branches are both acceptable — the test does not assert which | REQ-SYS-002 · VER-SYS-002 · C5 |
| IT-APP-020 | Row committed; CRM gate parked (sync pending) | Terminate the host process (OS-level kill), restart a new host on the same file | Row survives restart and is listable; the parked sync is lost — no outbox exists, so the restarted host performs **no** CRM attempt for the old row (only future creates sync) | REQ-DATA-003 · VER-DATA-003 · C5, C8 |

## 6. Integration cases — HTTP API (`IT-API`)

Full pipeline via `FIX-HTTP` over `FIX-DB`. All binding/routing/serialization
facts live here; unit-level permutations are not repeated.

| Case ID | Arrange / input | Act | Observable result | Traceability |
| --- | --- | --- | --- | --- |
| IT-API-001 | `POST /api/inquiries` with `FIX-DTO` (camelCase JSON) | POST | `201`; body has all eleven fields, camelCase, positive `id`, canonical `New`, UTC timestamps ending `Z`, nullable fields present as `null` when absent; `Location: /api/inquiries/{id}` GETs `200` with the same content; response is never a raw EF entity | REQ-API-001 · VER-API-001 · REQ-APP-001 · VER-APP-001 · C1, C3 |
| IT-API-002 | Representative invalid bodies (full permutations live in UT-VAL-002…008): one missing required field, one overlength field, one bad email, malformed JSON, absent body, JSON `null`, string where number expected | POST each | Every variant: `400` `application/problem+json` `ValidationProblemDetails` with an `errors` dictionary identifying the failing field or the body; attempted values are **not echoed**; wording/order/type URL not pinned | REQ-API-002 · VER-API-002 · REQ-SYS-005 · VER-SYS-005 · C1, C3 |
| IT-API-003 | `FIX-DTO` plus injected unknown properties: `id: 999`, `status: "Closed"`, `createdDate`, `updatedDate`, `adminNotes` | POST | `201`; returned row has server `id`, `status=New`, server timestamps from `FIX-TIME` — supplied server-owned fields have **no influence** (overposting ignored) | REQ-APP-001 · VER-APP-001 · C1 |
| IT-API-004 | `POST` with `Content-Type: text/plain`; separately `PATCH` on a known route | Send | `415` for the unsupported media type; `405` for the unsupported verb — produced by routing/HTTP machinery, not only by exception middleware | REQ-SYS-005 · VER-SYS-005 · C3 |
| IT-API-005 | GET/PUT/DELETE with `{id}` = `0`, `-1`, `abc`, and an Int32-overflow literal (`2147483648`) | Send | All fail routing with `404` — zero/negative ids cannot identify a row; non-integer and overflow never reach the service | REQ-API-003 · VER-API-003 · C3 |
| IT-API-006 | `PUT /api/inquiries/999999/status` (missing integer id) with an **invalid** status body (`"Bogus"`) | PUT | `400`, not `404` — validation runs before resource lookup | REQ-API-002 · VER-API-002 · C3 |
| IT-API-007 | GET existing id; GET missing valid-format id | GET | `200` `InquiryResponse` for the existing row; `404` `ProblemDetails` (matching numeric `status`, nonempty `title`, `type`) for the missing one | REQ-API-001 · VER-API-001 · REQ-API-003 · VER-API-003 · C3 |
| IT-API-008 | Seeded row; `PUT .../status` with `"  pEnDiNg  "` | PUT | `200`; response shows canonical `Pending`; stored/read-back name canonical (case-insensitive, whitespace-tolerant input only) | REQ-API-001 · VER-API-001 · REQ-APP-002 · VER-APP-002 · C2 |
| IT-API-009 | `PUT .../status` payload variants: status omitted, `null`, `0`, `1`, `"0"`, `"1"`, `true`, `[]`, `{}`, `"New,Closed"`, `"Draft"` | PUT each | Each returns `400` `ValidationProblemDetails`; a follow-up GET shows the status unchanged — never silently defaulted to `New` | REQ-APP-002 · VER-APP-002 · REQ-SYS-005 · VER-SYS-005 · C2, C3 |
| IT-API-010 | `FIX-TIME` advanced after creation; `PUT` the row's current status | PUT | `200` with the existing inquiry; `updatedDate` unchanged over HTTP — same-status update is a no-op end to end | REQ-APP-001 · VER-APP-001 · C2 |
| IT-API-011 | Valid status body, unknown id | `PUT .../status` | `404` `ProblemDetails`; row count unchanged | REQ-API-003 · VER-API-003 · C3 |
| IT-API-012 | DELETE existing id; then DELETE the same id again; separately DELETE a never-existing id | DELETE | First: `204` with empty body, row permanently gone; repeat and never-existing: `404` | REQ-API-001 · VER-API-001 · REQ-APP-003 · VER-APP-003 · C3 |
| IT-API-013 | Empty store | `GET /api/inquiries` | `200` envelope `{ items: [], page: 1, pageSize: 20, totalCount: 0 }` | REQ-API-004 · VER-API-004 · C4 |
| IT-API-014 | Seed > 20 rows, several sharing a `createdDate` via `FIX-TIME` | GET with defaults; then explicit `page=2&pageSize=5`; then `sort=createdDateAsc`; then `sort=createdDateDesc` | Defaults `page=1&pageSize=20&sort=createdDateDesc`; page 2 is the exact deterministic slice; both exact spellings of `sort` honored; ties ordered by `Id` in the sort direction; envelope echoes requested paging and filtered `totalCount` | REQ-API-004 · VER-API-004 · C4 |
| IT-API-015 | Seeded rows across statuses incl. `Closed` | GET with `status=Closed`; then combined with paging | Filter applied before count/paging: `totalCount` is the filtered count; `Closed` included when filtered, included in "all" when omitted; combined filter+page returns the correct slice + filtered total | REQ-API-004 · VER-API-004 · REQ-SYS-001 · C4 |
| IT-API-016 | Invalid query variants: `page=0`, `page=-1`, `page=abc`, `page=` blank, `pageSize=0`, `pageSize=101`, `pageSize=abc`, `page=2147483647&pageSize=100` (product overflows Int32), `status=` (supplied empty), `status=Draft`, `sort=newest` | GET each | Every variant `400` — never silently clamped; overflow rejected via a computation that does not itself overflow; `status=` (supplied) and unknown names rejected per C2 | REQ-API-004 · VER-API-004 · REQ-SYS-005 · VER-SYS-005 · C2, C4 |
| IT-API-017 | Seeded store; `page=99` (valid numbers, beyond the end) | GET | `200` with empty `items` and the filtered `totalCount` — not 404 | REQ-API-004 · VER-API-004 · C4 |
| IT-API-018 | `FIX-CRM` configured to fail permanently after commit | POST a valid inquiry | `201` with the created body — CRM failure after commit is **not** an HTTP error; row retrievable afterwards | REQ-CRM-001 · VER-CRM-001 · REQ-SYS-003 · C3, C5 |
| IT-API-019 | `FIX-INTERCEPT` forces a database failure on POST; run once with `UseEnvironment("Development")`, once with `"Production"`; `FIX-LOG` attached | POST | `500` sanitized `ProblemDetails` in **both** environments: no stack trace, SQL, paths, connection strings, exception message, or visitor values in the body; sink scan (`FIX-LOG.AssertPrivacy`) shows the same — including no EF command parameters with visitor values (sensitive-data logging disabled) and no request/response-body logging | REQ-SYS-002 · VER-SYS-002 · REQ-SYS-004 · VER-SYS-004 · REQ-SYS-005 · VER-SYS-005 · C3, C6 |
| IT-API-020 | Host with OpenAPI enabled | GET the swagger document; then execute the **documented** create request shape, then a documented status update, from it | Document lists all five endpoints (create, list, get-by-id, update-status, delete) such that the described create/update requests succeed against the API — behavior contract only; no exact schema snapshot, no property-order pinning | REQ-API-001 · VER-API-001 · REQ-API-005 · VER-API-005 · C3 |
| IT-API-021 | `POST` a valid inquiry; immediately `GET` by id and `GET` the list | Sequence | Created row readable by id and present in list results immediately — create-read-list round trip with no eventual-consistency gap | REQ-DATA-001 · VER-DATA-001 · REQ-SYS-002 · VER-SYS-002 · C8 |

## 7. Integration cases — migrations, schema, restart (`IT-DATA`)

Real startup migration path over `FIX-DB` temp files; `FIX-PROBE` for raw SQL.

| Case ID | Arrange / input | Act | Observable result | Traceability |
| --- | --- | --- | --- | --- |
| IT-DATA-001 | Brand-new empty database file | Run host startup (migrations applied before serving) | Migrations create the schema; host then serves requests normally; no `EnsureCreated` alongside migrations | REQ-DATA-003 · VER-DATA-003 · C8 |
| IT-DATA-002 | Database file with committed rows from a prior host run | Restart a new host on the **same file**; restart once more | Existing rows survive; second startup succeeds repeatably (migrations idempotent); no data reset | REQ-DATA-003 · VER-DATA-003 · C8 |
| IT-DATA-003 | Test-only migration appended after the real ones that throws deliberately | Start the host | Startup fails **fatally**: host does not serve, no silent fallback to an empty/in-memory store; failure surfaces as the migration exception | REQ-DATA-003 · VER-DATA-003 · C8 |
| IT-DATA-004 | Migrated schema (`FIX-PROBE` raw SQL) | Probe: insert duplicate email; insert status `'Draft'`; insert `NULL` into `firstName`; insert `NULL` into `message` | Duplicate email insert **succeeds** (email not unique); out-of-set status insert **fails** (status names constrained to the five C2 values); required columns NOT NULL, optional columns nullable; lengths are not probed here — request validation owns limits (SQLite need not enforce `HasMaxLength`) | REQ-DATA-003 · VER-DATA-003 · C1, C8 |
| IT-DATA-005 | UTC `datetime`-bearing row written at T0 | Read the raw stored value and reload via EF; assert the JSON representation after `FIX-HTTP` GET | Stored/EF-round-tripped instants equal T0; `Kind` restored to UTC where the provider drops it; API JSON keeps `Z` with no instant shift | REQ-DATA-001 · VER-DATA-001 · C8 |

*(Crash durability during pending CRM — process killed with a sync in flight —
is covered at IT-APP-020.)*

## 8. Traceability summary

| VER | Covered by (case IDs) |
| --- | --- |
| VER-API-001 | IT-API-001, 007, 008, 011, 012, 020; IT-APP-001, 003 |
| VER-API-002 | UT-VAL-001…008, 011; IT-API-002, 003, 006 |
| VER-API-003 | IT-API-005, 007, 011, 012; IT-APP-012 |
| VER-API-004 | IT-APP-008, 009; IT-API-013…017 |
| VER-API-005 | IT-API-020 |
| VER-APP-001 | IT-APP-001…007; IT-API-001, 003, 010 |
| VER-APP-002 | UT-VAL-009…012; IT-APP-003, 013; IT-API-008, 009 |
| VER-APP-003 (new) | IT-APP-010; IT-API-012 |
| VER-CRM-001 | IT-APP-014, 016; IT-API-018 |
| VER-CRM-002 | UT-CRM-001…005, 008, 009 |
| VER-CRM-003 | UT-CRM-001, 010 |
| VER-CRM-004 (new) | UT-CRM-004…007; IT-APP-017…019 |
| VER-DATA-001 | IT-APP-002, 007; IT-API-021; IT-DATA-005 |
| VER-DATA-003 (new) | IT-DATA-001…004; IT-APP-020 |
| VER-SYS-002 | IT-APP-014, 015, 019; IT-API-019, 021; IT-DATA-002 |
| VER-SYS-003 | IT-APP-014, 016; IT-API-018 — automated evidence for its `method=test` conversion; browser supplement out of scope here |
| VER-SYS-004 | UT-CRM-010; IT-API-019 |
| VER-SYS-005 | IT-API-002, 004…006, 011, 012, 016, 019 |

Explicitly **not** covered here (owned elsewhere): `VER-SYS-001`/`VER-UI-001`
(real-browser walkthroughs), `VER-UI-002` (frontend), `VER-DATA-002` (SQL Server
`database.sql` script and its three queries). Per the parent plan,
`VER-API-005`, `VER-SYS-003`, `VER-SYS-004`, and `VER-CRM-003` become
`method=test` entries with separate browser supplements; this catalog provides
their automated evidence only.

## 9. Known limitations of the planned evidence

- **UT-CRM-005 (all attempts time out):** with production settings the outer
  2 s budget always ends the slow sequence during the 400 ms backoff, so
  *exhaustion* of four per-attempt timeouts is unreachable by design (C6); the
  retry-on-attempt-timeout behavior is instead proven by UT-CRM-004, and the
  exhaustion rethrow path by UT-CRM-003's fast transient failures.
- **IT-APP-019 (ambiguous mid-write cancellation):** only weak invariants are
  assertable by design (C5); the commit-vs-abort branch is inherently
  nondeterministic and the test must accept both.
- **IT-DATA-003 (failing migration):** the failing step is a test-only migration
  in the test assembly; the production migration set itself is never corrupted.
- **IT-APP-020 (process kill):** OS-level kill semantics differ slightly across
  Linux/macOS/Windows; the asserted invariants (row survives, no sync replay)
  hold on all three.
