window.SYSTEM_MODEL = {
 "meta": {
  "name": "Course Inquiry Dashboard",
  "mode": "greenfield",
  "version": "0.1.0",
  "description": "Target design for Merrithew's internal course-inquiry tool: how a visitor's inquiry is captured, persisted, synced to a CRM, and worked by staff. Agreed in the planning session on 2026-09-16; no code exists yet.",
  "source_of_truth": "docs/architecture/model.json",
  "generated": "2026-09-16"
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
   "description": "Server-rendered page shell + layout. Serves the dashboard page and mounts the React island; holds no inquiry state of its own.",
   "responsibilities": [
    "Render layout and dashboard page",
    "Mount the React island",
    "Serve static assets from wwwroot"
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
   "description": "The interactive dashboard: inquiry list, status filter, detail view, status update, and success/error messages.",
   "responsibilities": [
    "List + filter inquiries",
    "Show inquiry detail",
    "Update status",
    "Surface success/error messages from ProblemDetails"
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
   "description": "DataAnnotations on the request DTOs. Runs at model binding; invalid input short-circuits to 400 ProblemDetails before any business logic.",
   "responsibilities": [
    "Required-field validation",
    "Email-format validation",
    "Known-status-value validation"
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
   "description": "Turns unhandled exceptions and missing-record cases into consistent ProblemDetails responses (400/404/500).",
   "responsibilities": [
    "Map NotFound to 404",
    "Map validation to 400",
    "Map unhandled to 500 without leaking internals"
   ],
   "evidence": [
    "backend/Middleware/"
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
   "description": "The business rules in one place: default status New, system-set timestamps, hard delete, filtered/paged queries, and triggering CRM sync after a create.",
   "responsibilities": [
    "Apply Status=New + timestamps on create",
    "Advance UpdatedDate on change",
    "Hard-delete an inquiry",
    "Filter/paginate/sort",
    "Orchestrate CRM sync"
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
   "description": "EF Core mapping and queries for CourseInquiry. The only writer to the store. Code-first; migrations create the schema.",
   "responsibilities": [
    "Map CourseInquiry entity",
    "Execute filtered/paged queries",
    "Persist changes in a single SaveChanges"
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
   "description": "Implements the ICrmClient port with a fake that stands in for a real CRM. Retries with exponential backoff; a final failure is caught, never propagated to the inquiry create.",
   "responsibilities": [
    "Accept inquiry data",
    "Retry with backoff on transient failure",
    "Return a structured success/failure result"
   ],
   "evidence": [
    "backend/Services/ICrmClient.cs",
    "backend/Services/SimulatedCrmClient.cs"
   ]
  },
  {
   "id": "comp.crmlog",
   "name": "Sync audit log",
   "level": 2,
   "kind": "service",
   "parent": "sub.crm",
   "tech": [
    "ILogger (structured)"
   ],
   "description": "Structured log of every CRM sync attempt and its outcome via the built-in logger. Email and phone are redacted so sensitive data never lands in logs.",
   "responsibilities": [
    "Record attempt + outcome + inquiry id",
    "Redact email/phone",
    "No DB table needed for the assessment"
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
   "description": "Submits a course inquiry from the public website. Unauthenticated; never sees the admin tool."
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
   "mechanism": "Razor renders the shell and a mount point; the Vite-built TS bundle (in wwwroot) boots the React island client-side."
  },
  {
   "id": "e.react.controller",
   "from": "comp.react",
   "to": "comp.controller",
   "label": "HTTPS/JSON - list, get, update, delete",
   "kind": "sync",
   "protocol": "HTTPS",
   "interface": "iface.rest",
   "mechanism": "fetch() with JSON. Errors arrive as ProblemDetails and are shown to the user as clear messages."
  },
  {
   "id": "e.controller.validation",
   "from": "comp.controller",
   "to": "comp.validation",
   "label": "validate DTO",
   "kind": "control",
   "mechanism": "DataAnnotations checked on model binding; invalid input returns 400 ProblemDetails before the action body runs."
  },
  {
   "id": "e.controller.errmw",
   "from": "comp.controller",
   "to": "comp.errmw",
   "label": "exceptions -> ProblemDetails",
   "kind": "control",
   "mechanism": "NotFound and unhandled exceptions are formatted centrally into consistent error responses."
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
   "mechanism": "Service builds IQueryable (Where(status) + OrderBy + Skip/Take) and persists via a single SaveChanges."
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
   "label": "SyncInquiryAsync",
   "kind": "async",
   "interface": "iface.crm",
   "mechanism": "Called AFTER the inquiry is persisted. The sync result is handled by the service; a CRM failure must not roll back or fail the create."
  },
  {
   "id": "e.crmclient.crm",
   "from": "comp.crmclient",
   "to": "ext.crm",
   "label": "push inquiry (simulated)",
   "kind": "sync",
   "protocol": "HTTPS",
   "mechanism": "Simulated call - no real endpoint/keys. Timeout + up to 3 retries with exponential backoff; after exhaustion the attempt is recorded as failed."
  },
  {
   "id": "e.crmclient.crmlog",
   "from": "comp.crmclient",
   "to": "comp.crmlog",
   "label": "record attempt + outcome",
   "kind": "control",
   "mechanism": "Structured log line with inquiry id and result; email and phone are redacted before logging."
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
   "description": "The five endpoints from the spec. JSON in/out, ProblemDetails for errors. The public website form and the React island are both clients of this contract."
  },
  {
   "id": "iface.crm",
   "name": "ICrmClient port",
   "provider": "comp.crmclient",
   "consumers": [
    "comp.service"
   ],
   "contract": "ICrmClient.SyncInquiryAsync(inquiry)",
   "description": "The seam between the app and the CRM. The service depends on this port, not on a vendor - so the simulated client can be swapped for a real one, and tests can force success or failure."
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
   "text": "A submitted inquiry is never silently lost: creation either persists the inquiry and returns it, or returns an error.",
   "allocated_to": [
    "sub.api",
    "sub.app",
    "sub.data"
   ],
   "rationale": "The Part 5 troubleshooting scenario - inquiries not appearing in the admin list - is precisely the failure the design must prevent (spec:88)."
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
   "text": "Sensitive inquirer data (email, phone) never appears in logs.",
   "allocated_to": [
    "sub.crm",
    "sub.app"
   ],
   "rationale": "Explicit requirement of the CRM integration and the security answer (spec:80, 98)."
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
   "text": "Required fields and email format are validated; violations return 400 with details.",
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
   "text": "The list endpoint supports pagination and sorting.",
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
   "text": "New inquiries default to status New; CreatedDate and UpdatedDate are system-set, and UpdatedDate advances on every change.",
   "allocated_to": [
    "comp.service"
   ],
   "rationale": "Mandated business rules (spec:45-46)."
  },
  {
   "id": "REQ-APP-002",
   "level": 1,
   "parent": "REQ-SYS-001",
   "text": "Status may be set to any valid status value in any order; unknown values are rejected.",
   "allocated_to": [
    "comp.service",
    "comp.validation"
   ],
   "rationale": "Free-form transitions decision (ADR-0008); staff correct statuses in both directions."
  },
  {
   "id": "REQ-DATA-001",
   "level": 1,
   "parent": "REQ-SYS-002",
   "text": "A created inquiry is persisted durably and appears in the list immediately after creation.",
   "allocated_to": [
    "comp.dbcontext",
    "comp.db"
   ],
   "rationale": "Directly addresses the troubleshooting scenario (spec:88)."
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
   "text": "A failed CRM sync is retried with exponential backoff before being recorded as failed.",
   "allocated_to": [
    "comp.crmclient"
   ],
   "rationale": "Retry-logic bonus (spec:82)."
  },
  {
   "id": "REQ-CRM-003",
   "level": 1,
   "parent": "REQ-SYS-004",
   "text": "Every CRM sync attempt is logged with its outcome, with email and phone redacted.",
   "allocated_to": [
    "comp.crmlog",
    "comp.crmclient"
   ],
   "rationale": "Log the attempt without exposing sensitive data (spec:78-80)."
  }
 ],
 "verifications": [
  {
   "id": "VER-SYS-001",
   "level": 0,
   "method": "demonstration",
   "name": "End-to-end triage walkthrough",
   "verifies": [
    "REQ-SYS-001"
   ],
   "status": "planned",
   "procedure": "Open the dashboard, filter by status, change an inquiry's status, and confirm the change is reflected."
  },
  {
   "id": "VER-SYS-002",
   "level": 0,
   "method": "test",
   "name": "Create-then-list persistence",
   "verifies": [
    "REQ-SYS-002"
   ],
   "status": "planned",
   "procedure": "POST an inquiry, then GET the list and assert it appears with the returned id."
  },
  {
   "id": "VER-SYS-003",
   "level": 0,
   "method": "analysis",
   "name": "CRM-failure isolation review",
   "verifies": [
    "REQ-SYS-003"
   ],
   "status": "planned",
   "procedure": "Trace the create path and show a CRM failure is caught after persistence and cannot roll back the inquiry."
  },
  {
   "id": "VER-SYS-004",
   "level": 0,
   "method": "inspection",
   "name": "Log redaction inspection",
   "verifies": [
    "REQ-SYS-004"
   ],
   "status": "planned",
   "procedure": "Trigger a sync and inspect log output; confirm no email or phone value is present."
  },
  {
   "id": "VER-SYS-005",
   "level": 0,
   "method": "test",
   "name": "Error-response integration",
   "verifies": [
    "REQ-SYS-005"
   ],
   "status": "planned",
   "procedure": "Assert 400 on invalid input and 404 on a missing record, each with a ProblemDetails body."
  },
  {
   "id": "VER-UI-001",
   "level": 1,
   "method": "demonstration",
   "name": "Dashboard behaviour walkthrough",
   "verifies": [
    "REQ-UI-001"
   ],
   "status": "planned",
   "procedure": "Exercise list, filter, detail, and status update; confirm success and error messages render."
  },
  {
   "id": "VER-API-001",
   "level": 1,
   "method": "test",
   "name": "Endpoint coverage",
   "verifies": [
    "REQ-API-001"
   ],
   "status": "planned",
   "procedure": "One test per endpoint hitting the happy path."
  },
  {
   "id": "VER-API-002",
   "level": 1,
   "method": "test",
   "name": "Validation rejects bad input",
   "verifies": [
    "REQ-API-002"
   ],
   "status": "planned",
   "procedure": "POST with a missing required field and with a malformed email; assert 400 + details each time."
  },
  {
   "id": "VER-API-003",
   "level": 1,
   "method": "test",
   "name": "Missing record returns 404",
   "verifies": [
    "REQ-API-003"
   ],
   "status": "planned",
   "procedure": "GET/PUT/DELETE an unknown id; assert 404 ProblemDetails."
  },
  {
   "id": "VER-API-004",
   "level": 1,
   "method": "test",
   "name": "Pagination and sorting",
   "verifies": [
    "REQ-API-004"
   ],
   "status": "planned",
   "procedure": "Seed several inquiries; assert page size and sort order are honoured."
  },
  {
   "id": "VER-API-005",
   "level": 1,
   "method": "inspection",
   "name": "Swagger documents all endpoints",
   "verifies": [
    "REQ-API-005"
   ],
   "status": "planned",
   "procedure": "Open Swagger UI; confirm every endpoint is documented and create/update is executable."
  },
  {
   "id": "VER-APP-001",
   "level": 1,
   "method": "test",
   "name": "Defaults and timestamps",
   "verifies": [
    "REQ-APP-001"
   ],
   "status": "planned",
   "procedure": "Create an inquiry -> Status=New and CreatedDate==UpdatedDate; update it -> UpdatedDate advances. Candidate for the required minimum test."
  },
  {
   "id": "VER-APP-002",
   "level": 1,
   "method": "test",
   "name": "Status value validation",
   "verifies": [
    "REQ-APP-002"
   ],
   "status": "planned",
   "procedure": "Update to a valid status (any order) succeeds; an unknown value is rejected."
  },
  {
   "id": "VER-DATA-001",
   "level": 1,
   "method": "test",
   "name": "Persistence round-trip",
   "verifies": [
    "REQ-DATA-001"
   ],
   "status": "planned",
   "procedure": "Save and reload an inquiry via EF Core; assert fields round-trip."
  },
  {
   "id": "VER-CRM-001",
   "level": 1,
   "method": "test",
   "name": "Create survives CRM failure",
   "verifies": [
    "REQ-CRM-001"
   ],
   "status": "planned",
   "procedure": "Force ICrmClient to throw; assert create still returns success and the inquiry is persisted. Candidate for the required minimum test."
  },
  {
   "id": "VER-CRM-002",
   "level": 1,
   "method": "test",
   "name": "Sync retries then succeeds",
   "verifies": [
    "REQ-CRM-002"
   ],
   "status": "planned",
   "procedure": "A client that fails twice then succeeds is retried and recorded as success."
  },
  {
   "id": "VER-CRM-003",
   "level": 1,
   "method": "inspection",
   "name": "Sync log redaction",
   "verifies": [
    "REQ-CRM-003"
   ],
   "status": "planned",
   "procedure": "Assert a sync log entry contains inquiry id + outcome but no email or phone."
  }
 ],
 "flows": [
  {
   "id": "flow.submit",
   "name": "Inquiry intake and CRM sync",
   "trigger": "A website visitor submits the course inquiry form.",
   "description": "The path that pays for the system: capture the inquiry durably first, then best-effort sync to the CRM. The order matters - persistence before sync is what makes REQ-SYS-002 and REQ-SYS-003 hold at once.",
   "steps": [
    {
     "n": 1,
     "from": "actor.visitor",
     "to": "comp.controller",
     "action": "POST /api/inquiries",
     "mechanism": "JSON body bound to a Create DTO; DataAnnotations validate required fields and email format."
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
     "mechanism": "Service sets Status=New and CreatedDate=UpdatedDate=UtcNow, then one SaveChanges."
    },
    {
     "n": 4,
     "from": "comp.dbcontext",
     "to": "comp.db",
     "action": "SQL INSERT",
     "mechanism": "EF Core -> SQLite. The inquiry is now durable and will appear in the list."
    },
    {
     "n": 5,
     "from": "comp.service",
     "to": "comp.crmclient",
     "action": "SyncInquiryAsync(inquiry)",
     "async": true,
     "condition": "inquiry persisted",
     "mechanism": "Only after the row is committed. A failure here is caught by the service, not surfaced to the caller."
    },
    {
     "n": 6,
     "from": "comp.crmclient",
     "to": "ext.crm",
     "action": "Push inquiry (simulated)",
     "mechanism": "Timeout + up to 3 retries with exponential backoff (Polly)."
    },
    {
     "n": 7,
     "from": "comp.crmclient",
     "to": "comp.crmlog",
     "action": "Record outcome",
     "async": true,
     "mechanism": "Structured log: inquiry id + success/failure; email and phone redacted."
    },
    {
     "n": 8,
     "from": "comp.controller",
     "to": "actor.visitor",
     "action": "201 Created + inquiry",
     "condition": "inquiry persisted",
     "mechanism": "Returns the created inquiry (id, Status=New). The CRM outcome does not gate this response."
    }
   ],
   "failure_modes": [
    {
     "when": "Validation fails (missing field or bad email)",
     "then": "400 ProblemDetails; nothing is written to the database.",
     "detected_by": "VER-API-002"
    },
    {
     "when": "The database write fails",
     "then": "Create returns an error; no partial inquiry is left because it is a single SaveChanges.",
     "detected_by": "VER-SYS-002"
    },
    {
     "when": "CRM sync fails after all retries",
     "then": "The inquiry stays created and persisted; the failure is logged; no error reaches the visitor.",
     "detected_by": "VER-CRM-001"
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
     "mechanism": "Razor renders the shell and the island mount point."
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
     "mechanism": "Single IQueryable: Where(status) + OrderBy + Skip/Take, executed once against SQLite."
    },
    {
     "n": 6,
     "from": "comp.react",
     "to": "comp.controller",
     "action": "PUT /api/inquiries/{id}/status",
     "mechanism": "Body validated as a known status enum value."
    },
    {
     "n": 7,
     "from": "comp.controller",
     "to": "comp.service",
     "action": "UpdateStatus(id, status)",
     "mechanism": "404 if the id is unknown; otherwise sets Status and advances UpdatedDate."
    },
    {
     "n": 8,
     "from": "comp.controller",
     "to": "comp.react",
     "action": "200 + updated inquiry",
     "condition": "id found and status valid",
     "mechanism": "React shows a success message and refreshes the row."
    }
   ],
   "failure_modes": [
    {
     "when": "Update targets an unknown id",
     "then": "404 ProblemDetails; the React island shows an error message.",
     "detected_by": "VER-API-003"
    },
    {
     "when": "Status value is not a known enum",
     "then": "400; the inquiry is left unchanged.",
     "detected_by": "VER-APP-002"
    }
   ]
  }
 ],
 "annotations": [
  {
   "id": "ann.greenfield",
   "target": "sys.dashboard",
   "kind": "note",
   "text": "Target design - no code exists yet. This model captures the plan agreed in the planning session on 2026-09-16. Verifications are all 'planned' for the same reason.",
   "author": "Planning session 2026-09-16"
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
   "text": "Sync attempts are recorded via structured logs (built-in ILogger) with email/phone redacted - not a database table. Sufficient for 'log that a sync was attempted' without extra schema.",
   "author": "Planning session 2026-09-16"
  },
  {
   "id": "ann.noauth",
   "target": "comp.controller",
   "kind": "note",
   "text": "No authentication is implemented; the staff endpoints are open. Access control is discussed in written-answers.md (Security). A real deployment would gate these endpoints.",
   "author": "Planning session 2026-09-16"
  },
  {
   "id": "ann.create-ui",
   "target": "comp.react",
   "kind": "question",
   "text": "Open: does the React island also handle create, or is create left to the public form + Swagger? Current plan: the island covers list/filter/detail/status; create via the API/Swagger.",
   "author": "Planning session 2026-09-16"
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
    "REQ-SYS-005"
   ],
   "sub.app": [
    "REQ-SYS-001",
    "REQ-SYS-002",
    "REQ-SYS-003",
    "REQ-SYS-004"
   ],
   "sub.data": [
    "REQ-SYS-002"
   ],
   "sub.crm": [
    "REQ-SYS-003",
    "REQ-SYS-004"
   ],
   "comp.react": [
    "REQ-UI-001"
   ],
   "comp.razor": [
    "REQ-UI-001"
   ],
   "comp.controller": [
    "REQ-API-001",
    "REQ-API-002",
    "REQ-API-003",
    "REQ-API-004",
    "REQ-API-005"
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
    "REQ-CRM-001"
   ],
   "comp.openapi": [
    "REQ-API-005"
   ],
   "comp.dbcontext": [
    "REQ-DATA-001"
   ],
   "comp.db": [
    "REQ-DATA-001"
   ],
   "comp.crmclient": [
    "REQ-CRM-001",
    "REQ-CRM-002",
    "REQ-CRM-003"
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
   "REQ-DATA-001": [
    "VER-DATA-001"
   ],
   "REQ-CRM-001": [
    "VER-CRM-001"
   ],
   "REQ-CRM-002": [
    "VER-CRM-002"
   ],
   "REQ-CRM-003": [
    "VER-CRM-003"
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
   "REQ-API-001": "traceability.html",
   "REQ-API-002": "traceability.html",
   "REQ-API-003": "traceability.html",
   "REQ-API-004": "traceability.html",
   "REQ-API-005": "traceability.html",
   "REQ-APP-001": "traceability.html",
   "REQ-APP-002": "traceability.html",
   "REQ-DATA-001": "traceability.html",
   "REQ-CRM-001": "traceability.html",
   "REQ-CRM-002": "traceability.html",
   "REQ-CRM-003": "traceability.html",
   "VER-SYS-001": "traceability.html",
   "VER-SYS-002": "traceability.html",
   "VER-SYS-003": "traceability.html",
   "VER-SYS-004": "traceability.html",
   "VER-SYS-005": "traceability.html",
   "VER-UI-001": "traceability.html",
   "VER-API-001": "traceability.html",
   "VER-API-002": "traceability.html",
   "VER-API-003": "traceability.html",
   "VER-API-004": "traceability.html",
   "VER-API-005": "traceability.html",
   "VER-APP-001": "traceability.html",
   "VER-APP-002": "traceability.html",
   "VER-DATA-001": "traceability.html",
   "VER-CRM-001": "traceability.html",
   "VER-CRM-002": "traceability.html",
   "VER-CRM-003": "traceability.html",
   "flow.submit": "flow--flow-submit.html",
   "flow.triage": "flow--flow-triage.html"
  }
 }
};
