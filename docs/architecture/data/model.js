window.SYSTEM_MODEL = {
 "meta": {
  "name": "Course Inquiry Dashboard",
  "mode": "greenfield",
  "version": "0.2.0",
  "description": "Design and implementation for Merrithew's internal course-inquiry tool, built and verified test-first through Phase 9. Boundary contracts: docs/architecture/contracts.md (ADR-0009). Test-first implementation and case catalogs: docs/testing/tdd-plan.md. Verification evidence is recorded on each verification in this model.",
  "source_of_truth": "docs/architecture/model.json",
  "generated": "2026-09-17"
 },
 "nodes": [
  {
   "id": "sys.dashboard",
   "name": "Course Inquiry Dashboard",
   "level": 0,
   "kind": "system",
   "parent": null,
   "owner": "Assessment submission",
   "description": "A single ASP.NET Core app that captures course inquiries, stores them, simulates pushing them to a CRM, and lets staff work the queue."
  },
  {
   "id": "sub.webui",
   "name": "Web UI",
   "level": 1,
   "kind": "subsystem",
   "parent": "sys.dashboard",
   "description": "What staff touch: a Razor Pages shell hosting a React + TypeScript island for the interactive dashboard.",
   "evidence": [
    "backend/ (Razor Pages + wwwroot)",
    "frontend/ (React + TS island)"
   ]
  },
  {
   "id": "sub.api",
   "name": "HTTP API",
   "level": 1,
   "kind": "subsystem",
   "parent": "sys.dashboard",
   "description": "The REST surface: five inquiry endpoints, request validation, error-to-ProblemDetails handling, and OpenAPI docs.",
   "evidence": [
    "backend/Controllers/"
   ]
  },
  {
   "id": "sub.app",
   "name": "Application",
   "level": 1,
   "kind": "subsystem",
   "parent": "sys.dashboard",
   "description": "Business logic and orchestration. The one place that knows the inquiry rules; controllers stay thin.",
   "evidence": [
    "backend/Services/"
   ]
  },
  {
   "id": "sub.data",
   "name": "Persistence",
   "level": 1,
   "kind": "subsystem",
   "parent": "sys.dashboard",
   "description": "EF Core over SQLite. Owns the CourseInquiries table and is the source of truth for every inquiry.",
   "evidence": [
    "backend/Models/",
    "database/database.sql"
   ]
  },
  {
   "id": "sub.crm",
   "name": "CRM Integration",
   "level": 1,
   "kind": "subsystem",
   "parent": "sys.dashboard",
   "description": "Isolates the rest of the app from the CRM behind a port, with retry and redacted logging. Simulated implementation.",
   "evidence": [
    "backend/Services/ (ICrmClient)"
   ]
  },
  {
   "id": "comp.razor",
   "name": "Razor Pages shell",
   "level": 2,
   "kind": "ui",
   "parent": "sub.webui",
   "tech": [
    "ASP.NET Core Razor Pages"
   ],
   "description": "Server-rendered layout and /dashboard mount point with loading/no-JavaScript guidance. Inquiry interaction requires React; the shell is not a server-rendered queue.",
   "responsibilities": [
    "Render layout and dashboard mount point",
    "Resolve Vite manifest entry, CSS and imports",
    "Serve production assets from a dedicated wwwroot subfolder"
   ],
   "evidence": [
    "backend/Pages/",
    "backend/wwwroot/"
   ]
  },
  {
   "id": "comp.react",
   "name": "React island",
   "level": 2,
   "kind": "ui",
   "parent": "sub.webui",
   "tech": [
    "React",
    "TypeScript",
    "Vite"
   ],
   "description": "List, filter, paging, detail and status update with safe accessible feedback. Handles stale responses and page reconciliation. Create/delete use API/Swagger, not island controls (contract C7).",
   "responsibilities": [
    "List + filter + page inquiries",
    "Show detail without stale-response overwrite",
    "Update status and reconcile filters/pages",
    "Surface safe accessible success/error feedback"
   ],
   "evidence": [
    "frontend/src/"
   ]
  },
  {
   "id": "comp.controller",
   "name": "Inquiries controller",
   "level": 2,
   "kind": "service",
   "parent": "sub.api",
   "tech": [
    "ASP.NET Core MVC"
   ],
   "description": "The five REST endpoints. Thin: binds and validates DTOs, delegates to the service, shapes HTTP responses. Supports status filter, pagination and sorting on the list.",
   "responsibilities": [
    "POST/GET/GET{id}/PUT{id}/status/DELETE{id}",
    "Status filter + pagination + sorting",
    "Map service results to status codes"
   ],
   "evidence": [
    "backend/Controllers/InquiriesController.cs"
   ]
  },
  {
   "id": "comp.validation",
   "name": "Request validation",
   "level": 2,
   "kind": "library",
   "parent": "sub.api",
   "tech": [
    "DataAnnotations"
   ],
   "description": "DataAnnotations plus explicit status name/presence checks on DTOs. Invalid binding/validation returns 400 ValidationProblemDetails before business logic. See contracts C1-C3.",
   "responsibilities": [
    "Required-field, length and email validation",
    "Reject missing/null/numeric/composite status",
    "Validate paging bounds without overflow"
   ],
   "evidence": [
    "backend/Models/Dtos/"
   ]
  },
  {
   "id": "comp.errmw",
   "name": "Error handling",
   "level": 2,
   "kind": "service",
   "parent": "sub.api",
   "tech": [
    "Exception middleware"
   ],
   "description": "Consistent API ProblemDetails for binding, validation, routing, missing resources and unhandled errors. Controllers map missing service results; status-code and exception handling cover their separate paths.",
   "responsibilities": [
    "Format 400/404/405/415 responses",
    "Sanitize 500s in Development and Production",
    "Avoid visitor data and raw exceptions in logs"
   ],
   "evidence": [
    "backend/Program.cs",
    "backend/Controllers/InquiriesController.cs"
   ]
  },
  {
   "id": "comp.openapi",
   "name": "OpenAPI / Swagger",
   "level": 2,
   "kind": "ui",
   "parent": "sub.api",
   "tech": [
    "Swashbuckle / OpenAPI"
   ],
   "description": "Generated API docs and an executable UI. Doubles as the create/update interface the spec permits in place of building every form.",
   "responsibilities": [
    "Document all endpoints + DTOs",
    "Provide manual create/update UI"
   ],
   "evidence": [
    "backend/Program.cs"
   ]
  },
  {
   "id": "comp.service",
   "name": "InquiryService",
   "level": 2,
   "kind": "service",
   "parent": "sub.app",
   "tech": [
    "C#"
   ],
   "description": "Forces New and UTC timestamps on create; applies free-form status changes (same-status no-op), hard deletion and deterministic filtered pages. Awaits bounded CRM sync only after commit (contracts C2-C6).",
   "responsibilities": [
    "Apply Status=New and one UTC instant on create",
    "Timestamp actual status changes, not no-ops",
    "Hard-delete without resurrection on concurrent deletion",
    "Filter/count/page/sort",
    "Isolate only post-commit CRM failures"
   ],
   "evidence": [
    "backend/Services/InquiryService.cs"
   ]
  },
  {
   "id": "comp.dbcontext",
   "name": "AppDbContext",
   "level": 2,
   "kind": "service",
   "parent": "sub.data",
   "tech": [
    "EF Core 10"
   ],
   "description": "EF Core mapping and SQLite queries for CourseInquiry. The service writes through this context; migrations run before requests. Fresh contexts and independent connections prove persistence and commit visibility.",
   "responsibilities": [
    "Map nullability, named statuses and UTC values",
    "Execute filtered count and ordered page queries",
    "Commit each mutation before external work"
   ],
   "evidence": [
    "backend/Models/AppDbContext.cs",
    "backend/Migrations/"
   ]
  },
  {
   "id": "comp.db",
   "name": "SQLite database",
   "level": 2,
   "kind": "datastore",
   "parent": "sub.data",
   "tech": [
    "SQLite"
   ],
   "description": "Single-file database holding the CourseInquiries table. Source of truth for what has been submitted. A SQL Server-dialect database.sql is provided alongside.",
   "evidence": [
    "database/database.sql"
   ]
  },
  {
   "id": "comp.crmclient",
   "name": "Simulated CRM client",
   "level": 2,
   "kind": "service",
   "parent": "sub.crm",
   "tech": [
    "C#",
    "Polly"
   ],
   "description": "Implements Task-returning ICrmClient. Real Polly pipeline surrounds deterministic simulated attempts; exceptions signal failure to the post-commit service boundary. No background worker or outbox.",
   "responsibilities": [
    "Accept committed inquiry data",
    "Retry transient errors at most three times after the original attempt",
    "Honor attempt/total timeouts and cancellation (C6)"
   ],
   "evidence": [
    "backend/Services/ICrmClient.cs",
    "backend/Services/SimulatedCrmClient.cs"
   ]
  },
  {
   "id": "comp.crmlog",
   "name": "Sync operational log",
   "level": 2,
   "kind": "service",
   "parent": "sub.crm",
   "tech": [
    "ILogger (structured)"
   ],
   "description": "Attempt and final-outcome metadata only. Omit all visitor fields and raw exceptions from messages, structured state, scopes and enabled telemetry. Not a durable business audit history.",
   "responsibilities": [
    "Record persisted inquiry id, attempt and outcome",
    "Prevent sensitive payloads reaching any configured sink",
    "No CRM log table required"
   ],
   "evidence": [
    "backend/Services/SimulatedCrmClient.cs"
   ]
  },
  {
   "id": "actor.visitor",
   "name": "Website Visitor",
   "kind": "person",
   "level": 0,
   "description": "Submits an inquiry from an external public form (out of scope). Visitor/staff are intended roles, not enforced access boundaries: this local assessment has no authentication."
  },
  {
   "id": "actor.staff",
   "name": "Merrithew Staff",
   "kind": "person",
   "level": 0,
   "description": "Internal user who triages inquiries: views, filters by status, and advances status as work progresses."
  },
  {
   "id": "ext.crm",
   "name": "External CRM",
   "kind": "external",
   "level": 0,
   "description": "Third-party business system inquiries are pushed to. SIMULATED for this assessment - no real endpoint or API keys.",
   "tech": [
    "Simulated"
   ]
  }
 ],
 "edges": [
  {
   "id": "e.visitor.controller",
   "from": "actor.visitor",
   "to": "comp.controller",
   "label": "HTTPS - POST /api/inquiries",
   "kind": "sync",
   "protocol": "HTTPS",
   "mechanism": "The public website form posts JSON to the create endpoint. The form itself is out of scope; the endpoint is the intake point."
  },
  {
   "id": "e.staff.razor",
   "from": "actor.staff",
   "to": "comp.razor",
   "label": "HTTPS - open dashboard",
   "kind": "sync",
   "protocol": "HTTPS",
   "mechanism": "Loads the Razor page that hosts the dashboard. No auth in this assessment (see written-answers.md, Security)."
  },
  {
   "id": "e.staff.openapi",
   "from": "actor.staff",
   "to": "comp.openapi",
   "label": "HTTPS - Swagger UI",
   "kind": "sync",
   "protocol": "HTTPS",
   "mechanism": "Manual create/update and API exploration during development and review."
  },
  {
   "id": "e.razor.react",
   "from": "comp.razor",
   "to": "comp.react",
   "label": "mount island",
   "kind": "control",
   "mechanism": "Razor resolves the Vite production manifest entry and CSS/imports from a dedicated wwwroot asset subfolder; React boots client-side without a Vite server."
  },
  {
   "id": "e.react.controller",
   "from": "comp.react",
   "to": "comp.controller",
   "label": "HTTPS/JSON - list, get, update",
   "kind": "sync",
   "protocol": "HTTPS",
   "interface": "iface.rest",
   "mechanism": "Same-origin fetch. The island handles ProblemDetails/non-JSON failures, suppresses stale responses, and refreshes the filtered page after mutations."
  },
  {
   "id": "e.controller.validation",
   "from": "comp.controller",
   "to": "comp.validation",
   "label": "validate DTO",
   "kind": "control",
   "mechanism": "JSON binding and DataAnnotations plus explicit status/query checks; invalid input returns 400 ValidationProblemDetails before the action executes."
  },
  {
   "id": "e.controller.errmw",
   "from": "comp.controller",
   "to": "comp.errmw",
   "label": "exceptions -> ProblemDetails",
   "kind": "control",
   "mechanism": "Controllers map missing-resource results; configured ProblemDetails, status-code and exception paths produce safe contract C3 responses."
  },
  {
   "id": "e.openapi.controller",
   "from": "comp.openapi",
   "to": "comp.controller",
   "label": "describe endpoints",
   "kind": "control",
   "mechanism": "OpenAPI generated from controller signatures and DTO annotations; no hand-maintained spec."
  },
  {
   "id": "e.controller.service",
   "from": "comp.controller",
   "to": "comp.service",
   "label": "call service",
   "kind": "sync",
   "mechanism": "Controller delegates every operation to the service; no business logic in the controller."
  },
  {
   "id": "e.service.dbcontext",
   "from": "comp.service",
   "to": "comp.dbcontext",
   "label": "CRUD + filtered/paged queries",
   "kind": "sync",
   "mechanism": "Service filters before count and paging; CreatedDate+Id order is deterministic. Count and page are separate queries; each mutation commits through SaveChangesAsync before CRM work."
  },
  {
   "id": "e.dbcontext.db",
   "from": "comp.dbcontext",
   "to": "comp.db",
   "label": "SQL - CourseInquiries",
   "kind": "data",
   "protocol": "SQLite",
   "mechanism": "EF Core provider writes to the SQLite file. Single writer; created via migrations."
  },
  {
   "id": "e.service.crmclient",
   "from": "comp.service",
   "to": "comp.crmclient",
   "label": "await SyncInquiryAsync",
   "kind": "sync",
   "interface": "iface.crm",
   "mechanism": "Awaited in the same request AFTER confirmed commit. CRM exceptions/cancellation cannot undo storage. A disconnected caller may not receive the created response (C5)."
  },
  {
   "id": "e.crmclient.crm",
   "from": "comp.crmclient",
   "to": "ext.crm",
   "label": "push inquiry (simulated)",
   "kind": "sync",
   "mechanism": "In-process only, not actual HTTPS. At most 4 attempts, 100/200/400ms backoff, 500ms attempt timeout and 2s total budget; cooperative cancellation. See C6."
  },
  {
   "id": "e.crmclient.crmlog",
   "from": "comp.crmclient",
   "to": "comp.crmlog",
   "label": "record attempt + outcome",
   "kind": "control",
   "mechanism": "Log safe metadata only. No visitor fields, raw exceptions or unsafe telemetry; inspect structured state, scopes and formatted output."
  }
 ],
 "interfaces": [
  {
   "id": "iface.rest",
   "name": "Inquiries REST API",
   "provider": "comp.controller",
   "consumers": [
    "comp.react",
    "comp.openapi"
   ],
   "contract": "OpenAPI (Swagger) - /api/inquiries",
   "description": "Five JSON endpoints with explicit wire, paging and error contracts in docs/architecture/contracts.md C1-C4. Create/delete remain available through API/Swagger; the public form is external."
  },
  {
   "id": "iface.crm",
   "name": "ICrmClient port",
   "provider": "comp.crmclient",
   "consumers": [
    "comp.service"
   ],
   "contract": "Task ICrmClient.SyncInquiryAsync(inquiry, CancellationToken)",
   "description": "Awaited post-commit seam. Completion means success; exceptions signal failure and are isolated by the service. Tests control outcomes while retry tests retain the real Polly pipeline."
  }
 ],
 "requirements": [
  {
   "id": "REQ-SYS-001",
   "level": 0,
   "text": "Staff can view inquiries, filter them by status, and update an inquiry's status from a single admin interface.",
   "allocated_to": [
    "sub.webui",
    "sub.api",
    "sub.app"
   ],
   "rationale": "The reason the tool exists (spec Scenario, lines 7 and 61-69)."
  },
  {
   "id": "REQ-SYS-002",
   "level": 0,
   "text": "A 201 response identifies a committed inquiry readable independently; definite pre-commit write failure reports an error and does not invoke CRM. A lost response can leave the caller uncertain about a committed write.",
   "allocated_to": [
    "sub.api",
    "sub.app",
    "sub.data"
   ],
   "rationale": "Troubleshooting scenario (spec:88), bounded by real commit/transport semantics in contract C5; no exactly-once intake promise."
  },
  {
   "id": "REQ-SYS-003",
   "level": 0,
   "text": "A CRM failure or outage never prevents an inquiry from being created and stored.",
   "allocated_to": [
    "sub.app",
    "sub.crm"
   ],
   "rationale": "The inquiry, not the CRM, is the record of truth; the CRM is a secondary, best-effort sync (spec:73-82)."
  },
  {
   "id": "REQ-SYS-004",
   "level": 0,
   "text": "Visitor data never appears in application logs, structured properties, scopes, or attached exceptions; CRM logs use safe metadata only.",
   "allocated_to": [
    "sub.crm",
    "sub.app",
    "sub.api"
   ],
   "rationale": "Privacy requirement (spec:80, 98), refined by contract C6 to cover telemetry and raw exception leaks."
  },
  {
   "id": "REQ-SYS-005",
   "level": 0,
   "text": "Invalid input and requests for missing records return clear, appropriate error responses.",
   "allocated_to": [
    "sub.api"
   ],
   "rationale": "Required backend behaviour plus the frontend's clear success/error messaging (spec:47, 69)."
  },
  {
   "id": "REQ-UI-001",
   "level": 1,
   "parent": "REQ-SYS-001",
   "text": "The dashboard lists inquiries, filters by status, shows inquiry detail, updates status, and shows clear success/error messages.",
   "allocated_to": [
    "comp.react",
    "comp.razor"
   ],
   "rationale": "The frontend acceptance list (spec:65-69)."
  },
  {
   "id": "REQ-UI-002",
   "level": 1,
   "parent": "REQ-SYS-001",
   "text": "The built dashboard loads from one host, handles stale requests and mutation/page recovery, and renders safe, keyboard-accessible feedback.",
   "allocated_to": [
    "comp.react",
    "comp.razor"
   ],
   "rationale": "Makes frontend acceptance (spec:65-69) and accessibility discussion (spec:100-102) testable; contract C7."
  },
  {
   "id": "REQ-API-001",
   "level": 1,
   "parent": "REQ-SYS-001",
   "text": "The API exposes create, list (with optional status filter), get-by-id, update-status, and delete endpoints.",
   "allocated_to": [
    "comp.controller"
   ],
   "rationale": "The required endpoint table (spec:31-37)."
  },
  {
   "id": "REQ-API-002",
   "level": 1,
   "parent": "REQ-SYS-005",
   "text": "Required fields, length bounds, email format and input shapes are validated before side effects; violations return safe 400 details.",
   "allocated_to": [
    "comp.validation",
    "comp.controller"
   ],
   "rationale": "Required validation (spec:44)."
  },
  {
   "id": "REQ-API-003",
   "level": 1,
   "parent": "REQ-SYS-005",
   "text": "A request for a non-existent inquiry returns 404.",
   "allocated_to": [
    "comp.errmw",
    "comp.controller"
   ],
   "rationale": "Appropriate errors for missing records (spec:47)."
  },
  {
   "id": "REQ-API-004",
   "level": 1,
   "parent": "REQ-SYS-001",
   "text": "Listing provides bounded, deterministic filtered pagination and an explicit envelope; invalid paging/sort values return 400 (contract C4).",
   "allocated_to": [
    "comp.controller",
    "comp.service"
   ],
   "rationale": "Committed bonus (spec:147)."
  },
  {
   "id": "REQ-API-005",
   "level": 1,
   "parent": "REQ-SYS-005",
   "text": "The API is documented via OpenAPI/Swagger, which also serves as a manual create/update interface.",
   "allocated_to": [
    "comp.openapi",
    "comp.controller"
   ],
   "rationale": "Permitted in place of building every form; committed bonus (spec:71, 147)."
  },
  {
   "id": "REQ-APP-001",
   "level": 1,
   "parent": "REQ-SYS-001",
   "text": "New inquiries force New and identical server-assigned UTC timestamps; actual status changes set UpdatedDate, same-status no-ops leave it unchanged. Timestamps are not monotonic versions.",
   "allocated_to": [
    "comp.service"
   ],
   "rationale": "Mandated business rules (spec:45-46)."
  },
  {
   "id": "REQ-APP-002",
   "level": 1,
   "parent": "REQ-SYS-001",
   "text": "Any defined status may replace any other; missing/null/numeric/composite or unknown wire values are rejected. Internal undefined enum values cannot persist.",
   "allocated_to": [
    "comp.service",
    "comp.validation"
   ],
   "rationale": "Free-form transitions decision (ADR-0008); staff correct statuses in both directions."
  },
  {
   "id": "REQ-APP-003",
   "level": 1,
   "parent": "REQ-SYS-001",
   "text": "DELETE permanently removes a row; Closed remains readable, reportable and reopenable. Concurrent deletion cannot resurrect a record.",
   "allocated_to": [
    "comp.service",
    "comp.controller"
   ],
   "rationale": "ADR-0006 and contracts C2-C4 clarify spec:37, 39."
  },
  {
   "id": "REQ-DATA-001",
   "level": 1,
   "parent": "REQ-SYS-002",
   "text": "Committed inquiries round-trip through fresh SQLite contexts, retain UTC instants and are readable by ID and through eligible list pages.",
   "allocated_to": [
    "comp.dbcontext",
    "comp.db"
   ],
   "rationale": "Directly addresses the troubleshooting scenario (spec:88)."
  },
  {
   "id": "REQ-DATA-002",
   "level": 1,
   "parent": "REQ-SYS-002",
   "text": "The SQL Server script executes matching DDL, at least five samples, and correct last-seven-days, status-count and duplicate-email queries.",
   "allocated_to": [
    "sub.data"
   ],
   "rationale": "Previously missing model coverage of mandatory spec:53-59; contract C8 requires actual SQL Server evidence."
  },
  {
   "id": "REQ-DATA-003",
   "level": 1,
   "parent": "REQ-SYS-002",
   "text": "Startup applies real SQLite migrations before serving; restart preserves data and migration failure is fatal, not an empty-store fallback.",
   "allocated_to": [
    "comp.dbcontext",
    "comp.db"
   ],
   "rationale": "ADR-0003 startup promise and contract C8."
  },
  {
   "id": "REQ-CRM-001",
   "level": 1,
   "parent": "REQ-SYS-003",
   "text": "Inquiry creation succeeds and persists even when the CRM sync fails; the failure is handled, not propagated.",
   "allocated_to": [
    "comp.service",
    "comp.crmclient"
   ],
   "rationale": "Success and failure handling of the sync (spec:78-79)."
  },
  {
   "id": "REQ-CRM-002",
   "level": 1,
   "parent": "REQ-SYS-003",
   "text": "Only transient CRM failures are retried with exponential backoff, stopping on success, permanent failure or retry exhaustion (contract C6).",
   "allocated_to": [
    "comp.crmclient"
   ],
   "rationale": "Retry-logic bonus (spec:82)."
  },
  {
   "id": "REQ-CRM-003",
   "level": 1,
   "parent": "REQ-SYS-004",
   "text": "Every started CRM attempt and terminal sync outcome is logged with safe inquiry/attempt metadata and no visitor payload or raw exception.",
   "allocated_to": [
    "comp.crmlog",
    "comp.crmclient"
   ],
   "rationale": "Log attempts without sensitive data (spec:78-80); contract C6 covers all sink channels."
  },
  {
   "id": "REQ-CRM-004",
   "level": 1,
   "parent": "REQ-SYS-003",
   "text": "CRM attempts and total execution have cooperative time budgets; caller cancellation stops attempts/backoff and cannot remove a committed inquiry.",
   "allocated_to": [
    "comp.crmclient",
    "comp.service"
   ],
   "rationale": "Makes the planned timeout/cancellation boundary explicit without promising forcible termination or durable delivery; C5-C6."
  }
 ],
 "verifications": [
  {
   "id": "VER-SYS-001",
   "level": 0,
   "method": "test",
   "name": "Integrated triage and browser acceptance",
   "verifies": [
    "REQ-SYS-001"
   ],
   "status": "passing",
   "evidence": "IT-UI-001..030, IT-HOST-001..004 (frontend/src test files; Integration/FrontendHostTests.cs) plus browser walkthroughs (docs/testing/manual-evidence.md incl. the Phase 9 built-app pass); Vitest 52/52 + xUnit non-SqlServer 146/146 + walkthrough PASS; @5d5fbe0 2026-09-17",
   "procedure": "Run mapped IT-UI/IT-HOST cases in docs/testing/frontend-and-sql-cases.md plus MAN-UI browser triage; component tests alone do not prove real mounting."
  },
  {
   "id": "VER-SYS-002",
   "level": 0,
   "method": "test",
   "name": "Commit and read-after-create boundaries",
   "verifies": [
    "REQ-SYS-002"
   ],
   "status": "passing",
   "evidence": "IT-APP-001..020 (commit visibility, definite failed write, repeat POST) + IT-API create/read/list cases (Integration/InquiryServiceTests.cs, InquiriesApiTests.cs, ProcessRestartTests.cs); xUnit non-SqlServer 146/146; @5d5fbe0 2026-09-17",
   "procedure": "Backend IT-API/IT-APP cases: POST then independent GET/list, definite failed write without CRM, commit visibility and repeated submissions. See docs/testing/backend-cases.md."
  },
  {
   "id": "VER-SYS-003",
   "level": 0,
   "method": "test",
   "name": "CRM-failure isolation",
   "verifies": [
    "REQ-SYS-003"
   ],
   "status": "passing",
   "evidence": "IT-APP-014 (no CRM before commit), IT-APP-016 (row survives CRM exhaustion) and neighboring post-commit cancellation cases, + IT-API-018 (201 + fetchable row despite CRM failure) (Integration/InquiryServiceTests.cs, InquiriesApiTests.cs); xUnit non-SqlServer 146/146; @5d5fbe0 2026-09-17",
   "procedure": "Backend IT-APP/IT-API: force CRM failure/cancellation only after independently observed commit; retain the row and created response where the connection remains usable."
  },
  {
   "id": "VER-SYS-004",
   "level": 0,
   "method": "test",
   "name": "Configured log sink privacy",
   "verifies": [
    "REQ-SYS-004"
   ],
   "status": "passing",
   "evidence": "UT-CRM terminal-scenario sink privacy + IT-API sanitized-500 cases (Unit/CrmPipelineTests.cs; Integration/InquiriesApiTests.cs; Fixtures/LogCaptureProvider.cs) + Phase 9 live log scan (sentinel email/phone/name 0 hits; no raw exceptions); xUnit non-SqlServer 146/146; @5d5fbe0 2026-09-17",
   "procedure": "Backend UT-CRM and IT-API: inject sensitive field/exception sentinels and inspect rendered logs, structured state, scopes and exceptions through configured sinks, including telemetry."
  },
  {
   "id": "VER-SYS-005",
   "level": 0,
   "method": "test",
   "name": "Error-response integration",
   "verifies": [
    "REQ-SYS-005"
   ],
   "status": "passing",
   "evidence": "IT-API validation/binding/404/media-type/sanitized-500 cases in Development and Production (Integration/InquiriesApiTests.cs) + Phase 9 walkthrough 400/404 probes; xUnit non-SqlServer 146/146; @5d5fbe0 2026-09-17",
   "procedure": "Backend IT-API: validation/binding/resource/routing/media-type errors and sanitized 500s in Development and Production; inspect ProblemDetails without pinning framework wording."
  },
  {
   "id": "VER-UI-001",
   "level": 1,
   "method": "test",
   "name": "Dashboard behavior",
   "verifies": [
    "REQ-UI-001"
   ],
   "status": "passing",
   "evidence": "IT-UI list/filter/page/detail/status/feedback cases (frontend/src/components/*.test.tsx) + Phase 7/9 browser walkthroughs (docs/testing/manual-evidence.md); Vitest 52/52 + walkthrough PASS; @5d5fbe0 2026-09-17",
   "procedure": "Frontend IT-UI component integration: list/filter/page/detail/status and clear feedback. Supplement with MAN-UI browser walkthroughs."
  },
  {
   "id": "VER-UI-002",
   "level": 1,
   "method": "test",
   "name": "UI async boundaries and actual host assets",
   "verifies": [
    "REQ-UI-002"
   ],
   "status": "passing",
   "evidence": "UT-UI-001..005 + IT-UI stale-response/keyboard/live-region cases (frontend/src test files) + IT-HOST-001..004 compiled-asset hosting (Integration/FrontendHostTests.cs); Vitest 52/52 + xUnit non-SqlServer 146/146 + keyboard/focus walkthrough PASS; @5d5fbe0 2026-09-17",
   "procedure": "Frontend UT-UI, IT-UI and IT-HOST: controlled stale responses, recovery, safe text, keyboard/focus/live regions and real compiled Razor assets. Browser checks cover actual mounting/layout."
  },
  {
   "id": "VER-API-001",
   "level": 1,
   "method": "test",
   "name": "Endpoint workflows",
   "verifies": [
    "REQ-API-001"
   ],
   "status": "passing",
   "evidence": "IT-API-001..021 five-endpoint workflows (Integration/InquiriesApiTests.cs) + clean-checkout smoke (201+Location, 200 envelope, 204, 404 problem+json); xUnit non-SqlServer 146/146; @5d5fbe0 2026-09-17",
   "procedure": "Backend IT-API: all five endpoints through the real HTTP pipeline, including Location/readback, response DTOs, updates and permanent deletion."
  },
  {
   "id": "VER-API-002",
   "level": 1,
   "method": "test",
   "name": "Validation and wire-input boundaries",
   "verifies": [
    "REQ-API-002"
   ],
   "status": "passing",
   "evidence": "UT-VAL-001..011 + DTO-HTTP-001..010 + IT-API binding/validation cases (Unit/CreateInquiryValidationTests.cs, UpdateStatusDtoValidationTests.cs, StatusParsingTests.cs; Integration/DtoHttpTests.cs, InquiriesApiTests.cs); xUnit non-SqlServer 146/146; @5d5fbe0 2026-09-17",
   "procedure": "Backend UT-VAL rules plus IT-API representative HTTP binding: missing/blank/length/email/type errors, optional fields and ignored overposting; invalid requests cause no write or sync."
  },
  {
   "id": "VER-API-003",
   "level": 1,
   "method": "test",
   "name": "Missing resources and deletion races",
   "verifies": [
    "REQ-API-003"
   ],
   "status": "passing",
   "evidence": "IT-API missing/invalid-id, repeat-delete and deletion-race mapping cases (Integration/InquiriesApiTests.cs) + Phase 9 404 probe + clean-checkout DELETE 204 then 404 problem+json; xUnit non-SqlServer 146/146; @5d5fbe0 2026-09-17",
   "procedure": "Backend IT-API: GET/PUT/DELETE unknown IDs, invalid route IDs, second DELETE, validation-before-lookup and controlled concurrent deletion return contract C3 errors."
  },
  {
   "id": "VER-API-004",
   "level": 1,
   "method": "test",
   "name": "Filtered deterministic pagination",
   "verifies": [
    "REQ-API-004"
   ],
   "status": "passing",
   "evidence": "IT-APP filtered-paging/sorting + IT-API query-boundary cases (Integration/InquiryServiceTests.cs, InquiriesApiTests.cs) + Phase 9 walkthrough paging (2 pages, disabled end controls); xUnit non-SqlServer 146/146; @5d5fbe0 2026-09-17",
   "procedure": "Backend IT-APP/IT-API: mixed statuses/tied dates, both sort directions, exact page and filtered total, empty/past-end pages and invalid/overflowing inputs."
  },
  {
   "id": "VER-API-005",
   "level": 1,
   "method": "test",
   "name": "OpenAPI contract and Swagger exercise",
   "verifies": [
    "REQ-API-005"
   ],
   "status": "passing",
   "evidence": "IT-API OpenAPI-document case (Integration/InquiriesApiTests.cs) + Phase 9 Swagger UI manual create/update (201 then 200, verified by GET); xUnit non-SqlServer 146/146 + walkthrough PASS; @5d5fbe0 2026-09-17",
   "procedure": "Backend IT-API inspects generated OpenAPI operations, DTO/status/page shapes and responses; manually create/update through Swagger. Avoid a whole-document snapshot."
  },
  {
   "id": "VER-APP-001",
   "level": 1,
   "method": "test",
   "name": "Server-owned status and UTC timestamps",
   "verifies": [
    "REQ-APP-001"
   ],
   "status": "passing",
   "evidence": "IT-APP-001 (forced New, CreatedDate==UpdatedDate, fresh-context re-read: InquiryServiceTests.cs:42-52), IT-APP-003 (UpdatedDate advances, CreatedDate immutable: :101-102), IT-API-001/010 repeat the contract over HTTP (InquiriesApiTests.cs:50-52, 239-242); xUnit non-SqlServer 146/146; @5d5fbe0 2026-09-17",
   "procedure": "Backend IT-APP/IT-API: fixed-time create, overposting isolation, later actual update, no-op, frozen/backward clock and fresh-context UTC readback."
  },
  {
   "id": "VER-APP-002",
   "level": 1,
   "method": "test",
   "name": "Free-form status and explicit validity",
   "verifies": [
    "REQ-APP-002"
   ],
   "status": "passing",
   "evidence": "UT-VAL status parsing/membership + IT-APP free-form/no-op/guard + IT-API invalid-status 400 cases (Unit/UpdateStatusDtoValidationTests.cs, StatusParsingTests.cs; Integration/InquiryServiceTests.cs, InquiriesApiTests.cs); xUnit non-SqlServer 146/146; @5d5fbe0 2026-09-17",
   "procedure": "Backend UT-VAL/IT-APP/IT-API: corrections both ways including reopening; reject omitted/null/numeric/composite/unknown wire values and invalid internal enums without mutation."
  },
  {
   "id": "VER-APP-003",
   "level": 1,
   "method": "test",
   "name": "Hard delete versus retained Closed",
   "verifies": [
    "REQ-APP-003"
   ],
   "status": "passing",
   "evidence": "IT-APP hard-delete/retained-Closed/concurrency cases + IT-API repeat-delete 404 (Integration/InquiryServiceTests.cs, InquiriesApiTests.cs); xUnit non-SqlServer 146/146 + clean-checkout DELETE/404 smoke; @5d5fbe0 2026-09-17",
   "procedure": "Backend IT-APP/IT-API: deleted rows disappear from fresh reads/counts, repeat delete is 404, Closed remains queryable/reopenable, deletion races never resurrect rows."
  },
  {
   "id": "VER-DATA-001",
   "level": 1,
   "method": "test",
   "name": "SQLite persistence round-trip",
   "verifies": [
    "REQ-DATA-001"
   ],
   "status": "passing",
   "evidence": "IT-DATA-001..005 fresh-context round-trips (Integration/PersistenceTests.cs) + IT-API read-after-create (InquiriesApiTests.cs); xUnit non-SqlServer 146/146; @5d5fbe0 2026-09-17",
   "procedure": "Backend IT-DATA and create/read IT-API: real migrations/provider, fresh-context Unicode/null/status/UTC round-trip and no false success from tracked entities."
  },
  {
   "id": "VER-DATA-002",
   "level": 1,
   "method": "test",
   "name": "SQL Server DDL and report execution",
   "verifies": [
    "REQ-DATA-002"
   ],
   "status": "passing",
   "evidence": "IT-SQL-001..008 (SqlServer/CourseInquirySqlScriptTests.cs): database.sql verbatim + three report queries on disposable SQL Server 2022 (docker mcr.microsoft.com/mssql/server:2022-latest; CourseInquiryTests_* databases dropped, zero leftovers); xUnit SqlServer 8/8; @5d5fbe0 2026-09-17",
   "procedure": "IT-SQL in docs/testing/frontend-and-sql-cases.md: execute shipped script/query bodies on disposable SQL Server; assert schema/samples, fixed seven-day cutoffs, status totals and normalized duplicate groups. Missing engine blocks evidence."
  },
  {
   "id": "VER-DATA-003",
   "level": 1,
   "method": "test",
   "name": "Startup migrations and durable restart",
   "verifies": [
    "REQ-DATA-003"
   ],
   "status": "passing",
   "evidence": "IT-DATA startup-migration/restart/fatal-failure cases (Integration/PersistenceTests.cs, ProcessRestartTests.cs, TestOnlyFatalMigration.cs) + clean-checkout first-run migration (backend/inquiries.db created before listening); xUnit non-SqlServer 146/146; @5d5fbe0 2026-09-17",
   "procedure": "Backend IT-DATA: boot on an empty temporary SQLite file, create rows, restart against the same file, and force fatal migration failure without fallback."
  },
  {
   "id": "VER-CRM-001",
   "level": 1,
   "method": "test",
   "name": "Creation survives post-commit CRM failure",
   "verifies": [
    "REQ-CRM-001"
   ],
   "status": "passing",
   "evidence": "IT-APP-016 (row survives CRM exhaustion, no exception escapes create: InquiryServiceTests.cs:408-431), IT-APP-014 (no CRM before commit: :354-373), IT-API-018 (201 + fetchable row: InquiriesApiTests.cs:387-400); xUnit non-SqlServer 146/146; @5d5fbe0 2026-09-17",
   "procedure": "Backend IT-APP/IT-API: injected transient/permanent/unexpected CRM exceptions cannot remove an independently committed row or turn a connected create into a CRM error."
  },
  {
   "id": "VER-CRM-002",
   "level": 1,
   "method": "test",
   "name": "Transient retry and exhaustion",
   "verifies": [
    "REQ-CRM-002"
   ],
   "status": "passing",
   "evidence": "UT-CRM-001..010 on the real Polly pipeline (Unit/CrmPipelineTests.cs): transient retry then success, 4-attempt exhaustion, 100/200/400 ms backoff, permanent-failure no-retry; xUnit non-SqlServer 146/146; @5d5fbe0 2026-09-17",
   "procedure": "Backend UT-CRM: real Polly pipeline with controlled attempts/time; transient failure then success, maximum four attempts, 100/200/400ms backoff and no permanent-error retry."
  },
  {
   "id": "VER-CRM-003",
   "level": 1,
   "method": "test",
   "name": "Safe attempt and outcome logging",
   "verifies": [
    "REQ-CRM-003"
   ],
   "status": "passing",
   "evidence": "UT-CRM sink privacy across terminal scenarios (Unit/CrmPipelineTests.cs, Fixtures/LogCaptureProvider.cs) + IT-API sanitized-500 + Phase 9 live log scan (CRM lines carry inquiry id/attempt/outcome only); xUnit non-SqlServer 146/146; @5d5fbe0 2026-09-17",
   "procedure": "Backend UT-CRM/IT-API: started attempts and final outcomes contain persisted ID and safe metadata, never sensitive payloads or raw exceptions on any sink channel."
  },
  {
   "id": "VER-CRM-004",
   "level": 1,
   "method": "test",
   "name": "Bounded timeout and cancellation",
   "verifies": [
    "REQ-CRM-004"
   ],
   "status": "passing",
   "evidence": "UT-CRM attempt/total timeout and cancellation cases + IT-APP post-commit cancellation durability (Unit/CrmPipelineTests.cs; Integration/InquiryServiceTests.cs); xUnit non-SqlServer 146/146; @5d5fbe0 2026-09-17",
   "procedure": "Backend UT-CRM and IT-APP: attempt timeout, outer total budget, cancellation before/during attempts or backoff, no subsequent retries, and preserved post-commit data."
  }
 ],
 "flows": [
  {
   "id": "flow.submit",
   "name": "Inquiry intake and CRM sync",
   "trigger": "A website visitor submits the course inquiry form.",
   "description": "Commit first, then await bounded best-effort CRM in the same request. CRM failure cannot undo storage; transport loss can hide a commit and a crash can lose the sync. No exactly-once delivery (C5).",
   "steps": [
    {
     "n": 1,
     "from": "actor.visitor",
     "to": "comp.controller",
     "action": "POST /api/inquiries",
     "mechanism": "JSON bound to a create-only DTO; required fields, lengths and email checked. Client-supplied server-owned/unknown fields are ignored (C1)."
    },
    {
     "n": 2,
     "from": "comp.controller",
     "to": "comp.service",
     "action": "CreateInquiry(dto)",
     "condition": "validation passed",
     "mechanism": "Invalid input already returned 400 ProblemDetails; only valid DTOs reach the service."
    },
    {
     "n": 3,
     "from": "comp.service",
     "to": "comp.dbcontext",
     "action": "Insert inquiry",
     "mechanism": "Service forces New and samples UTC once for both timestamps, then awaits SaveChangesAsync with the request token."
    },
    {
     "n": 4,
     "from": "comp.dbcontext",
     "to": "comp.db",
     "action": "Commit SQL INSERT",
     "mechanism": "SQLite commit completes before CRM; an independent connection can read the row. A failed/disconnected response is not proof the write did not commit."
    },
    {
     "n": 5,
     "from": "comp.service",
     "to": "comp.crmclient",
     "action": "await SyncInquiryAsync(inquiry, ct)",
     "condition": "inquiry committed; cancellation may skip CRM",
     "mechanism": "Same request, not fire-and-forget. Catch CRM failures/cancellation only after the durable write; never roll back or repeat the insert."
    },
    {
     "n": 6,
     "from": "comp.crmclient",
     "to": "ext.crm",
     "action": "Push inquiry (in-process simulation)",
     "mechanism": "Real Polly: transient-only retry, at most 4 attempts, 100/200/400ms delays, 500ms attempt and 2s total cooperative timeouts (C6)."
    },
    {
     "n": 7,
     "from": "comp.crmclient",
     "to": "comp.crmlog",
     "action": "Record safe outcome",
     "mechanism": "Persisted inquiry id + attempt/outcome metadata; no visitor fields, raw exceptions or unsafe telemetry."
    },
    {
     "n": 8,
     "from": "comp.controller",
     "to": "actor.visitor",
     "action": "201 + Location + InquiryResponse",
     "condition": "inquiry committed and response connection usable",
     "mechanism": "CRM outcome does not change the success response, but awaiting it adds bounded latency. A disconnected caller may never receive confirmation."
    }
   ],
   "failure_modes": [
    {
     "when": "Validation fails (missing field or bad email)",
     "then": "400 ProblemDetails; nothing is written to the database.",
     "detected_by": "VER-API-002"
    },
    {
     "when": "Definite database failure before commit",
     "then": "Sanitized 500 if response is possible; no partial row or CRM attempt.",
     "detected_by": "VER-SYS-002"
    },
    {
     "when": "CRM sync fails after commit",
     "then": "Row remains durable; safe outcome logged; connected caller still receives 201.",
     "detected_by": "VER-CRM-001"
    },
    {
     "when": "Cancellation before insert or during post-commit CRM",
     "then": "Before insert: no row/sync. After commit: stop CRM cooperatively, preserve row. Transport response is not guaranteed.",
     "detected_by": "VER-CRM-004"
    },
    {
     "when": "Response lost or process stops after commit",
     "then": "Caller may be uncertain and repeated POST may duplicate intake; CRM delivery can be lost without an outbox.",
     "detected_by": "VER-SYS-002"
    }
   ]
  },
  {
   "id": "flow.triage",
   "name": "Staff triage: filter and update status",
   "trigger": "Staff open the dashboard to work the inquiry queue.",
   "description": "The daily-use path. Reads are filtered and paged; a status change is a single dedicated endpoint with clear success/error feedback.",
   "steps": [
    {
     "n": 1,
     "from": "actor.staff",
     "to": "comp.razor",
     "action": "GET dashboard",
     "mechanism": "Razor renders layout, loading/no-JavaScript guidance and a single island mount point."
    },
    {
     "n": 2,
     "from": "comp.razor",
     "to": "comp.react",
     "action": "Mount island",
     "mechanism": "Vite-built TS bundle from wwwroot boots and fetches data client-side."
    },
    {
     "n": 3,
     "from": "comp.react",
     "to": "comp.controller",
     "action": "GET /api/inquiries?status=New&page=1",
     "mechanism": "Filtered + paged list request; a ProblemDetails response is shown as an error message."
    },
    {
     "n": 4,
     "from": "comp.controller",
     "to": "comp.service",
     "action": "List(filter, page, sort)",
     "mechanism": "Applies the status filter, sort, and pagination."
    },
    {
     "n": 5,
     "from": "comp.service",
     "to": "comp.dbcontext",
     "action": "Query",
     "mechanism": "Filter before count and page; CreatedDate then Id ordering. Count and page are separate SQLite queries, not a concurrent snapshot (C4)."
    },
    {
     "n": 6,
     "from": "comp.react",
     "to": "comp.controller",
     "action": "PUT /api/inquiries/{id}/status",
     "mechanism": "Required named status validated explicitly; reject omitted/null/numeric/composite values (C2)."
    },
    {
     "n": 7,
     "from": "comp.controller",
     "to": "comp.service",
     "action": "UpdateStatus(id, status)",
     "mechanism": "404 if missing/deleted during write; otherwise set status and UTC update time only for an actual change. Same-status PUT is a no-op; last committed write wins."
    },
    {
     "n": 8,
     "from": "comp.controller",
     "to": "comp.react",
     "action": "200 + updated inquiry",
     "condition": "id found and status valid",
     "mechanism": "Show saved feedback and refresh filter/page/detail state. A row may leave the filter; reconcile empty last pages and ignore stale responses. A refresh failure is not a failed save."
    }
   ],
   "failure_modes": [
    {
     "when": "Update targets an unknown id",
     "then": "404 ProblemDetails; the React island shows an error message.",
     "detected_by": "VER-API-003"
    },
    {
     "when": "Status input is missing, numeric, composite or unknown",
     "then": "400 before lookup; row remains unchanged.",
     "detected_by": "VER-APP-002"
    },
    {
     "when": "Older list/detail response arrives last",
     "then": "Ignore stale data and preserve the newer selection; failures leave recoverable UI state.",
     "detected_by": "VER-UI-002"
    }
   ]
  }
 ],
 "annotations": [
  {
   "id": "ann.greenfield",
   "target": "sys.dashboard",
   "kind": "note",
   "text": "Designed greenfield, then implemented test-first through Phase 9; every verification now carries its passing evidence in-model (see each verification's evidence field and docs/testing/tdd-plan.md). Contracts in docs/architecture/contracts.md and TDD/cases in docs/testing/.",
   "author": "Planning session 2026-09-16; verified 2026-09-17"
  },
  {
   "id": "ann.delete",
   "target": "comp.service",
   "kind": "decision",
   "text": "DELETE is a hard delete (row removed). Chosen over soft-delete/archive because the Closed status already covers 'keep but deactivate', so a second archival mechanism would be redundant at this scope. See ADR-0006.",
   "author": "Planning session 2026-09-16"
  },
  {
   "id": "ann.crmport",
   "target": "comp.crmclient",
   "kind": "decision",
   "text": "The CRM is reached only through the ICrmClient port; the implementation is simulated. This keeps the vendor swappable and lets tests force success/failure. See ADR-0007.",
   "author": "Planning session 2026-09-16"
  },
  {
   "id": "ann.sqlite",
   "target": "comp.db",
   "kind": "risk",
   "text": "SQLite is used for run-anywhere simplicity, but database.sql is authored in SQL Server dialect. Watch the differences (INT IDENTITY vs INTEGER AUTOINCREMENT, datetime types). See ADR-0003.",
   "author": "Planning session 2026-09-16"
  },
  {
   "id": "ann.log",
   "target": "comp.crmlog",
   "kind": "decision",
   "text": "Operational attempt/outcome logs use only safe metadata, omitting visitor fields and raw exceptions across state/scopes/telemetry. Not a durable business audit trail; see C6.",
   "author": "Planning session 2026-09-16"
  },
  {
   "id": "ann.noauth",
   "target": "comp.controller",
   "kind": "note",
   "text": "No authentication: staff/visitor labels and same origin do not restrict access. Local synthetic-data demo only. Production auth/retention/CSRF strategy remains out of scope (C7).",
   "author": "Planning session 2026-09-16"
  },
  {
   "id": "ann.create-ui",
   "target": "comp.react",
   "kind": "decision",
   "text": "Resolved: island = list/filter/page/detail/status; create/delete via API/Swagger. Public visitor form is external. ADR-0009, C7.",
   "author": "Boundary review 2026-09-16"
  },
  {
   "id": "ann.tdd",
   "target": "sys.dashboard",
   "kind": "decision",
   "text": "Every committed behavior follows red-green-refactor. Unit and real-provider/HTTP/component/SQL integrations are specified in docs/testing/tdd-plan.md and case catalogs; browser checks supplement automation. SQL Server is required for full script evidence, not runtime.",
   "author": "Boundary review 2026-09-16"
  }
 ],
 "index": {
  "reqs_for_node": {
   "sub.webui": [
    "REQ-SYS-001"
   ],
   "sub.api": [
    "REQ-SYS-001",
    "REQ-SYS-002",
    "REQ-SYS-004",
    "REQ-SYS-005"
   ],
   "sub.app": [
    "REQ-SYS-001",
    "REQ-SYS-002",
    "REQ-SYS-003",
    "REQ-SYS-004"
   ],
   "sub.data": [
    "REQ-SYS-002",
    "REQ-DATA-002"
   ],
   "sub.crm": [
    "REQ-SYS-003",
    "REQ-SYS-004"
   ],
   "comp.react": [
    "REQ-UI-001",
    "REQ-UI-002"
   ],
   "comp.razor": [
    "REQ-UI-001",
    "REQ-UI-002"
   ],
   "comp.controller": [
    "REQ-API-001",
    "REQ-API-002",
    "REQ-API-003",
    "REQ-API-004",
    "REQ-API-005",
    "REQ-APP-003"
   ],
   "comp.validation": [
    "REQ-API-002",
    "REQ-APP-002"
   ],
   "comp.errmw": [
    "REQ-API-003"
   ],
   "comp.service": [
    "REQ-API-004",
    "REQ-APP-001",
    "REQ-APP-002",
    "REQ-APP-003",
    "REQ-CRM-001",
    "REQ-CRM-004"
   ],
   "comp.openapi": [
    "REQ-API-005"
   ],
   "comp.dbcontext": [
    "REQ-DATA-001",
    "REQ-DATA-003"
   ],
   "comp.db": [
    "REQ-DATA-001",
    "REQ-DATA-003"
   ],
   "comp.crmclient": [
    "REQ-CRM-001",
    "REQ-CRM-002",
    "REQ-CRM-003",
    "REQ-CRM-004"
   ],
   "comp.crmlog": [
    "REQ-CRM-003"
   ]
  },
  "vers_for_req": {
   "REQ-SYS-001": [
    "VER-SYS-001"
   ],
   "REQ-SYS-002": [
    "VER-SYS-002"
   ],
   "REQ-SYS-003": [
    "VER-SYS-003"
   ],
   "REQ-SYS-004": [
    "VER-SYS-004"
   ],
   "REQ-SYS-005": [
    "VER-SYS-005"
   ],
   "REQ-UI-001": [
    "VER-UI-001"
   ],
   "REQ-UI-002": [
    "VER-UI-002"
   ],
   "REQ-API-001": [
    "VER-API-001"
   ],
   "REQ-API-002": [
    "VER-API-002"
   ],
   "REQ-API-003": [
    "VER-API-003"
   ],
   "REQ-API-004": [
    "VER-API-004"
   ],
   "REQ-API-005": [
    "VER-API-005"
   ],
   "REQ-APP-001": [
    "VER-APP-001"
   ],
   "REQ-APP-002": [
    "VER-APP-002"
   ],
   "REQ-APP-003": [
    "VER-APP-003"
   ],
   "REQ-DATA-001": [
    "VER-DATA-001"
   ],
   "REQ-DATA-002": [
    "VER-DATA-002"
   ],
   "REQ-DATA-003": [
    "VER-DATA-003"
   ],
   "REQ-CRM-001": [
    "VER-CRM-001"
   ],
   "REQ-CRM-002": [
    "VER-CRM-002"
   ],
   "REQ-CRM-003": [
    "VER-CRM-003"
   ],
   "REQ-CRM-004": [
    "VER-CRM-004"
   ]
  },
  "flows_for_node": {
   "actor.visitor": [
    "flow.submit"
   ],
   "comp.controller": [
    "flow.submit",
    "flow.triage"
   ],
   "comp.service": [
    "flow.submit",
    "flow.triage"
   ],
   "comp.dbcontext": [
    "flow.submit",
    "flow.triage"
   ],
   "comp.db": [
    "flow.submit"
   ],
   "comp.crmclient": [
    "flow.submit"
   ],
   "ext.crm": [
    "flow.submit"
   ],
   "comp.crmlog": [
    "flow.submit"
   ],
   "actor.staff": [
    "flow.triage"
   ],
   "comp.razor": [
    "flow.triage"
   ],
   "comp.react": [
    "flow.triage"
   ]
  },
  "children": {
   "sys.dashboard": [
    "sub.webui",
    "sub.api",
    "sub.app",
    "sub.data",
    "sub.crm"
   ],
   "sub.webui": [
    "comp.razor",
    "comp.react"
   ],
   "sub.api": [
    "comp.controller",
    "comp.validation",
    "comp.errmw",
    "comp.openapi"
   ],
   "sub.app": [
    "comp.service"
   ],
   "sub.data": [
    "comp.dbcontext",
    "comp.db"
   ],
   "sub.crm": [
    "comp.crmclient",
    "comp.crmlog"
   ]
  },
  "page_for": {
   "sys.dashboard": "index.html",
   "sub.webui": "sub--sub-webui.html",
   "sub.api": "sub--sub-api.html",
   "sub.app": "sub--sub-app.html",
   "sub.data": "sub--sub-data.html",
   "sub.crm": "sub--sub-crm.html",
   "comp.razor": "sub--sub-webui.html",
   "comp.react": "sub--sub-webui.html",
   "comp.controller": "sub--sub-api.html",
   "comp.validation": "sub--sub-api.html",
   "comp.errmw": "sub--sub-api.html",
   "comp.openapi": "sub--sub-api.html",
   "comp.service": "sub--sub-app.html",
   "comp.dbcontext": "sub--sub-data.html",
   "comp.db": "sub--sub-data.html",
   "comp.crmclient": "sub--sub-crm.html",
   "comp.crmlog": "sub--sub-crm.html",
   "actor.visitor": "index.html",
   "actor.staff": "index.html",
   "ext.crm": "index.html",
   "REQ-SYS-001": "traceability.html",
   "REQ-SYS-002": "traceability.html",
   "REQ-SYS-003": "traceability.html",
   "REQ-SYS-004": "traceability.html",
   "REQ-SYS-005": "traceability.html",
   "REQ-UI-001": "traceability.html",
   "REQ-UI-002": "traceability.html",
   "REQ-API-001": "traceability.html",
   "REQ-API-002": "traceability.html",
   "REQ-API-003": "traceability.html",
   "REQ-API-004": "traceability.html",
   "REQ-API-005": "traceability.html",
   "REQ-APP-001": "traceability.html",
   "REQ-APP-002": "traceability.html",
   "REQ-APP-003": "traceability.html",
   "REQ-DATA-001": "traceability.html",
   "REQ-DATA-002": "traceability.html",
   "REQ-DATA-003": "traceability.html",
   "REQ-CRM-001": "traceability.html",
   "REQ-CRM-002": "traceability.html",
   "REQ-CRM-003": "traceability.html",
   "REQ-CRM-004": "traceability.html",
   "VER-SYS-001": "traceability.html",
   "VER-SYS-002": "traceability.html",
   "VER-SYS-003": "traceability.html",
   "VER-SYS-004": "traceability.html",
   "VER-SYS-005": "traceability.html",
   "VER-UI-001": "traceability.html",
   "VER-UI-002": "traceability.html",
   "VER-API-001": "traceability.html",
   "VER-API-002": "traceability.html",
   "VER-API-003": "traceability.html",
   "VER-API-004": "traceability.html",
   "VER-API-005": "traceability.html",
   "VER-APP-001": "traceability.html",
   "VER-APP-002": "traceability.html",
   "VER-APP-003": "traceability.html",
   "VER-DATA-001": "traceability.html",
   "VER-DATA-002": "traceability.html",
   "VER-DATA-003": "traceability.html",
   "VER-CRM-001": "traceability.html",
   "VER-CRM-002": "traceability.html",
   "VER-CRM-003": "traceability.html",
   "VER-CRM-004": "traceability.html",
   "flow.submit": "flow--flow-submit.html",
   "flow.triage": "flow--flow-triage.html"
  }
 }
};
