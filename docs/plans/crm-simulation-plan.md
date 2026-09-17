# Runtime CRM Simulation Demonstration — Phase 10 Plan

- **Status:** complete — runtime at `74eafc9`, coverage at `16478d2`, island control at `cb3cbf4`; six-mode browser walkthrough and docs done
- **Scope:** expose the simulated CRM outcome (`Success`, `TransientThenSuccess`, `AlwaysTransientFailure`, `PermanentFailure`, `Timeout`, `InternalCancellation`) for selection at runtime — by configuration, by HTTP, and from the dashboard UI — without weakening any Part 4 invariant
- **Related:** [ADR-0007](../architecture/adr/0007-crm-port-retry-logging.md), [contracts C5–C6](../architecture/contracts.md), [TODO Phase 10](../../TODO.md)

## 1. Context

Part 4 of the assessment asks for a simulated CRM integration: accept inquiry data, log that a sync was attempted, handle success and failure, keep sensitive data out of logs, and (bonus) demonstrate retry/structured error handling. Through Phase 9 that was satisfied by `SimulatedCrmClient`'s default always-succeed operation plus test-injected outcomes: the `simulation` constructor parameter let the suite script failures through the real Polly pipeline, but a reviewer running the app saw only the success path.

Phase 10 makes the outcome selectable outside tests while keeping every load-bearing rule:

- **Persist first, sync second** (C5): the inquiry commits before any CRM work, and no CRM failure can roll it back or fail the create.
- **Bounded, private CRM work** (C6): the Polly pipeline (4 attempts, 100/200/400 ms backoff, 500 ms per-attempt timeout, 2 s total budget) and the allow-listed log state stay exactly as verified.
- **Dev surfaces are opt-in**: new endpoints and UI appear in Development or behind an explicit flag, never silently in production.

## 2. What already landed (commit `74eafc9`)

| Change | File | Notes |
| --- | --- | --- |
| `CrmSimulationMode`/`CrmSyncOutcome` enums, `CrmSimulationOptions` (bound to the `CrmSimulation` config section, validated at startup), immutable `CrmSimulationSettings`, `CrmInquiryPayload`, `CrmSyncResult`, and the thread-safe `CrmSimulationRuntime` | `backend/Services/CrmSimulation.cs` | Settings are snapshotted per sync, so a mid-flight dev-tool change cannot alter an in-flight retry sequence. Results retain bounded (≤100) non-PII metadata only. |
| Production constructor over `CrmSimulationRuntime`; payload-aware and legacy test seams retained | `backend/Services/SimulatedCrmClient.cs` | The simulated external boundary now receives an explicit `CrmInquiryPayload` (mapped once per sync) instead of the EF entity; logging still accepts only inquiry id/attempt/outcome/error type. |
| CRM-originated cancellation isolated at the post-commit boundary | `backend/Services/InquiryService.cs` | Closes the gap where an `OperationCanceledException` thrown by the CRM itself (caller token not cancelled) escaped `CreateAsync` after the commit. |
| `/api/dev/crm-simulation` endpoint group | `backend/DevTools/CrmSimulationEndpoints.cs` | `GET /` catalog+settings, `PUT /` validated update, `GET /results/{inquiryId}` safe result. Mapped only when `DevToolsOptions.CrmSimulationEnabled` (Development or `DevTools:CrmSimulation=true`). |
| `CrmSimulation` defaults + `DevTools:CrmSimulation` key | `backend/appsettings.json`, `backend/DevTools/DevToolsOptions.cs` | `ValidateDataAnnotations` + `ValidateOnStart` fail fast on bad config. |

**Runtime smoke evidence** (Development host, real `POST /api/inquiries`): `TransientThenSuccess` → `201`, result `Success` after 3 attempts with 2 logged transient retries; `PermanentFailure` → `201`, result `Failed` after 1 attempt, row intact; `InternalCancellation` → `201`, result `Cancelled` after 1 attempt, row intact. Logs contained only inquiry id/attempt/outcome/error type.

## 3. Delivered follow-up work

Everything below landed after the runtime commit, as planned.

### 3.1 Automated coverage (backend)

Delivered as `CrmRuntimeSimulationTests` (a dedicated file; the legacy seam suite
stays untouched) via a counting-runtime subclass:

| Case | Mode | Assert |
| --- | --- | --- |
| UT-CRM-011 | `Success` | 1 attempt, success outcome, payload reached the boundary (id matches) |
| UT-CRM-012 | `TransientThenSuccess` (n=2, latency 0) | 3 attempts, exponential gaps, terminal success |
| UT-CRM-013 | `AlwaysTransientFailure` | 4 attempts then `HttpRequestException` rethrow, `Failed` result recorded |
| UT-CRM-014 | `PermanentFailure` | 1 attempt, no retry, `Failed` result |
| UT-CRM-015 | `Timeout` | cooperative timeouts until the 2 s budget, `TimedOut` result, ≤3 attempts |
| UT-CRM-016 | `InternalCancellation` | 1 attempt, `Cancelled` result, no retry |
| UT-CRM-017 | all modes | `AssertPrivacy` over captured logs with visitor sentinels (incl. payload-bearing assertions) |

Integration cases (`tests/CourseInquiryDashboard.Tests/Integration/`):

- Dev endpoints mapped in the fixture (it already runs `Development`): `GET /` shape, `PUT /` round-trip and validation 400 (`ValidationProblemDetails`) for out-of-range values and unknown modes, `GET /results/{id}` 404 for unknown ids.
- `POST /api/inquiries` under `InternalCancellation` returns `201` and the row is readable from a fresh context — pins the new isolation behavior.
- A production-shaped host (`DevTools:CrmSimulation` unset) must **not** map `/api/dev/crm-simulation` (404).

### 3.2 Island control (frontend)

- `frontend/src/lib/crmSimulation.ts`: typed client with runtime shape validation (`parse*` guards — no unchecked casts), `crmSimulationToolsEnabled()` reading `window.__crmSimulationTools`.
- `frontend/src/components/CrmSimulationControl.tsx`: mode select (with per-mode description), `Failures first` (0–3, shown for `TransientThenSuccess`), `Latency (ms)` (0–400), **Apply** (update settings only) and **Run demo inquiry** (apply → real `POST /api/inquiries` → safe result readback → queue refresh). Status line states outcome and attempt count and always notes the inquiry is kept.
- `frontend/src/App.tsx` + `backend/Pages/Dashboard.cshtml(.cs)`: render inside a shared dev-tools area beside `ScenarioSwitcher`, gated on `Model.CrmSimulationToolsEnabled` setting `window.__crmSimulationTools = true` — the same shell-flag pattern scenario seeding uses, so UI and endpoints cannot drift.
- `frontend/src/vite-env.d.ts`: declare both shell flags on `Window`.

Component tests (`*.test.tsx` next to the control, fetch-double pattern like the existing suites): catalog load + render, apply-failure wording, run-demo success, CRM-failure result wording, unavailable-result fallback, disabled/busy states, and no mount-time request when the flag is off.

### 3.3 Verification

1. `dotnet test` — all existing cases stay green (the legacy `Func<CancellationToken, Task>` seam keeps UT-CRM-001..010 compiling unchanged).
2. `pnpm test` and `pnpm run build` in `frontend/`.
3. Browser walkthrough on the running app: exercise all six modes through the control, confirm queue refresh shows the demo inquiry, retry backoff visible in server logs, PII sentinel scan stays clean, control absent with the flag off.
4. Record evidence per the TDD-plan conventions.

### 3.4 Docs

- `docs/backend/README.md`: runtime modes, config keys, endpoint contract, privacy guarantees.
- `docs/frontend/README.md`: the dev control and its flag.
- ADR-0007 addendum noting the runtime simulation does not change the accepted best-effort delivery policy (the [durable outbox](../architecture/future/durable-crm-outbox.md) note still applies).
- `docs/CHANGELOG.md` entry under Added.

## 4. Explicit non-goals

- No durable delivery: a process stop between commit and sync still loses the sync (outbox remains a future design).
- No new public API surface: `/api/dev/crm-simulation` is dev-only and never mapped by default outside Development.
- No CRM payload logging: `CrmInquiryPayload` is passed to the simulated boundary and never to `ILogger`.
- No success/`crmSynced` field on `InquiryResponse`: intake stays decoupled from CRM outcome.
