# Repository Guidelines

## Project Overview

The **Merrithew Course Inquiry Dashboard** is an internal web application designed for Merrithew staff to manage and triage course registration inquiries submitted by website visitors. It provides a queue interface to inspect incoming leads, filter by workflow status, update progress, and simulate pushing inquiry data to an external CRM system.

The repository is organized around a greenfield target design defined across 8 Architecture Decision Records (`docs/architecture/adr/`), a domain specification (`docs/domain/course-inquiry.md`), interactive architecture diagrams (`docs/architecture/model.json`), and a phased implementation roadmap (`TODO.md`).

---

## Architecture & Data Flow

The system runs as a single deployable ASP.NET Core process hosting five integrated subsystems:

```
[Website Visitor]              [Merrithew Staff]
       │                               │
       │ POST /api/inquiries           │ GET /dashboard
       ▼                               ▼
┌──────────────┐             ┌─────────────────────────────────┐
│   HTTP API   │             │  Web UI (sub.webui)             │
│  (sub.api)   │◄────────────│  Razor Shell (comp.razor)       │
└──────┬───────┘             │   └── React Island (comp.react) │
       │                     └─────────────────────────────────┘
       ▼
┌──────────────────────────────────────┐
│  Application Layer (sub.app)         │
│  InquiryService (comp.service)       │
└──────┬───────────────────────────────┘
       │
       ├─── (1. Durably Persist) ───► ┌───────────────────────────┐
       │                              │  Persistence (sub.data)   │
       │                              │  EF Core 10 + SQLite      │
       │                              └───────────────────────────┘
       │
       └─── (2. Best-Effort Sync) ──► ┌───────────────────────────┐
                                      │  CRM Integration (sub.crm)│
                                      │  ICrmClient Port          │
                                      │  └── Polly Retry Policy   │
                                      │  └── Redacted ILogger     │
                                      └───────────────────────────┘
```

### Key Modules & Responsibilities
- **Web UI (`sub.webui`, ADR-0004)**: Server-rendered Razor Pages shell providing the outer layout and mounting an interactive React + TypeScript island compiled via Vite directly into `backend/wwwroot`.
- **HTTP API (`sub.api`, ADR-0002, ADR-0005)**: Attribute-routed MVC controllers exposing 5 REST endpoints, validating request DTOs via DataAnnotations, mapping errors to RFC 7807 `ProblemDetails`, and exposing OpenAPI/Swagger UI.
- **Application (`sub.app`, ADR-0005)**: Central `InquiryService` managing business invariants, timestamps, status defaulting, filtering/pagination, and CRM push orchestration.
- **Persistence (`sub.data`, ADR-0003)**: EF Core Code-First managing a local SQLite database (`AppDbContext`) with automatic startup migrations, paired with a companion SQL Server-dialect script (`database/database.sql`).
- **CRM Integration (`sub.crm`, ADR-0007)**: Decoupled `ICrmClient` port seam. The `SimulatedCrmClient` executes transient retries via Polly (exponential backoff) and emits structured log entries with sensitive visitor data redacted.

### Critical Data Flow: "Persist First, Sync Second"
The intake workflow (`flow.submit`) enforces a load-bearing architectural invariant:
1. **Validation**: `POST /api/inquiries` validates `CreateInquiryDto`. Format errors short-circuit to `400 ValidationProblemDetails`.
2. **Persistence**: `InquiryService` forces `Status = Status.New`, stamps `CreatedDate = UpdatedDate = DateTime.UtcNow`, and commits to SQLite via `AppDbContext.SaveChangesAsync()`.
3. **CRM Sync**: Only after database commit succeeds, `InquiryService` invokes `ICrmClient.SyncInquiryAsync()`.
4. **Resilience & Fault Isolation**: CRM sync failures or timeout retries are logged and caught within `InquiryService`. CRM errors **never** throw out of the service or roll back the stored inquiry (`REQ-SYS-003`).

### Triage Data Flow (`flow.triage`)
1. Staff loads `/dashboard`; Razor Pages serves the layout and mounts the React bundle.
2. React island executes same-origin `GET /api/inquiries?status=...&page=...` requests.
3. Status updates via `PUT /api/inquiries/{id}/status` are **free-form** (ADR-0008), allowing staff to transition between any valid `Status` enum values in any order.
4. Inquiry deletions via `DELETE /api/inquiries/{id}` perform a permanent **hard delete** (ADR-0006); archival without deletion is achieved via `Status.Closed`.

---

## Key Directories

```text
merrithew-course-web-app/
├── backend/                  # ASP.NET Core Web API & Razor Pages project
│   ├── Controllers/          # Thin REST controllers (InquiriesController.cs)
│   ├── Models/               # Domain entity (CourseInquiry.cs) and AppDbContext.cs
│   │   └── Dtos/             # Input/output DTOs (CreateInquiryDto, UpdateStatusDto, InquiryResponse)
│   ├── Services/             # Business logic (InquiryService.cs) and CRM port (ICrmClient.cs)
│   ├── Pages/                # Razor Pages host shell and layout
│   └── wwwroot/              # Static build target for frontend Vite assets
├── frontend/                 # React + TypeScript client project
│   └── src/                  # React island components (inquiry table, filters, detail drawer)
├── database/                 # SQL Server-dialect script (database.sql) with DDL and sample queries
├── tests/                    # xUnit automated test project (CourseInquiryDashboard.Tests)
└── docs/
    ├── architecture/         # System diagrams, model.json, and ADRs (0001–0008)
    │   └── adr/              # Immutable architectural decision records
    └── domain/               # Domain models, invariants, and vocabulary rules
```

---

## Development Commands

### Dependency Restore
```bash
# Restore .NET dependencies (backend & tests)
dotnet restore

# Install frontend dependencies
cd frontend && pnpm install
```

### Build
```bash
# Build frontend bundle into backend/wwwroot/
cd frontend && pnpm run build

# Build .NET solution
dotnet build
```

### Run & Dev
```bash
# Run backend (hosts API and frontend)
dotnet run --project backend

# Run with hot reload
dotnet watch --project backend
```
- Web Application & Dashboard: `http://localhost:5000` (or `https://localhost:5001`)
- OpenAPI / Swagger UI: `http://localhost:5000/swagger`

### Database Migrations
```bash
# Add an EF Core migration
dotnet ef migrations add <MigrationName> --project backend

# Apply migrations to local SQLite database
dotnet ef database update --project backend
```

### Testing
```bash
# Run all automated tests
dotnet test

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run a specific test class
dotnet test --filter "FullyQualifiedName~InquiryServiceTests"
```

---

## Code Conventions & Common Patterns

### Layering & Separation of Concerns (ADR-0005)
- **Thin Controllers**: Controllers handle HTTP semantics, route parameters, model validation checks, and status code mapping. Business rules and database access reside exclusively in `InquiryService`.
- **No Direct DbContext in Controllers**: Controllers inject `IInquiryService` and `ILogger<InquiriesController>`; they never inject `AppDbContext`.
- **No Repository Layer**: EF Core's `DbSet<CourseInquiry>` acts as the repository; do not introduce redundant abstraction layers over `AppDbContext`.

### DTOs & Mapping
- **Entity Isolation**: Never expose `CourseInquiry` directly in controller parameters or responses. Use `CreateInquiryDto`, `UpdateStatusDto`, and `InquiryResponse`.
- **Validation**: Enforce constraints on DTOs using `System.ComponentModel.DataAnnotations`:
  ```csharp
  public class CreateInquiryDto
  {
      [Required]
      public string FirstName { get; set; } = string.Empty;

      [Required]
      public string LastName { get; set; } = string.Empty;

      [Required, EmailAddress]
      public string Email { get; set; } = string.Empty;

      public string? Phone { get; set; }

      [Required]
      public string CourseName { get; set; } = string.Empty;

      public string? PreferredLocation { get; set; }
      public string? Message { get; set; }
  }
  ```

### Error Handling & ProblemDetails
- Return standard RFC 7807 `ProblemDetails` responses:
  - `400 Bad Request`: Model validation failures or unparseable status enums.
  - `404 Not Found`: Missing inquiry IDs on `GET`, `PUT`, or `DELETE`.
  - `500 Internal Server Error`: Unhandled exceptions, sanitized to avoid leaking database or server stack traces.

### Asynchronous Operations
- All I/O operations (database queries, external port calls) must be fully asynchronous:
  ```csharp
  public async Task<InquiryResponse> CreateInquiryAsync(CreateInquiryDto dto, CancellationToken ct = default);
  ```
- Always pass `CancellationToken` through controller actions down to `SaveChangesAsync(ct)` and `SyncInquiryAsync(..., ct)`.

### Dependency Injection (DI)
Register components in `backend/Program.cs`:
```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IInquiryService, InquiryService>();
builder.Services.AddScoped<ICrmClient, SimulatedCrmClient>();
```

### Logging & PII Redaction (ADR-0007)
- Use standard `ILogger<T>`.
- **Strict PII Redaction Rule**: Never emit raw visitor `Email` or `Phone` in log messages. Mask sensitive values prior to logging:
  - Email: `j***@domain.com`
  - Phone: `***-***-1234`

### State Management & Frontend Architecture
- **Server**: Stateless REST API; the Razor Pages shell serves the host HTML container (`<div id="dashboard-root"></div>`) without holding session state.
- **Client**: React functional components with standard hooks (`useState`, `useEffect`, `useCallback`) managing inquiry data, active filter selection, pagination index, modal visibility, and mutation loading/error states.
- **Networking**: Frontend communicates with same-origin `/api/inquiries` via `fetch()`, avoiding CORS configuration.

---

## Important Files

| File Path | Description & Purpose |
| --- | --- |
| `backend/Program.cs` | Application entry point: configures DI, middleware pipeline, EF Core SQLite, Swagger, and endpoint routing. |
| `backend/Controllers/InquiriesController.cs` | REST API controller exposing the 5 core inquiry endpoints. |
| `backend/Models/CourseInquiry.cs` | Core domain entity (`Id`, `FirstName`, `LastName`, `Email`, `Phone`, `CourseName`, `Status`, timestamps). |
| `backend/Models/Dtos/` | Request and response contracts (`CreateInquiryDto.cs`, `UpdateStatusDto.cs`, `InquiryResponse.cs`). |
| `backend/Services/InquiryService.cs` | Orchestration layer: implements business rules, default status, timestamps, and triggers CRM sync. |
| `backend/Services/ICrmClient.cs` | Port abstraction for external CRM communication. |
| `backend/Services/SimulatedCrmClient.cs` | Simulated CRM implementation wrapped with Polly retry logic. |
| `database/database.sql` | SQL Server DDL script with table creation, $\ge 5$ sample rows, and 3 analytical queries. |
| `frontend/vite.config.ts` | Frontend build configuration targeting `backend/wwwroot/`. |
| `docs/domain/course-inquiry.md` | Domain specifications, vocabulary definitions, and business rule checklist. |
| `docs/architecture/adr/` | Architectural decisions (ADR 0001 through 0008). |
| `docs/architecture/model.json` | Single source of truth for architectural models, sequence flows, and verification matrix. |
| `TODO.md` | Phased implementation task checklist linked to verification IDs. |
| `written-answers.md` | Technical assessment responses covering Troubleshooting, Security, Accessibility, and Code Quality. |

---

## Runtime/Tooling Preferences

- **Backend Runtime**: **.NET 10 SDK** (LTS, supported through November 2028). Required for compilation and runtime.
- **Frontend Tooling**: **Node.js LTS** with **pnpm** (preferred package manager for Vite build execution per ADR-0004).
- **Database Engine**: **SQLite** (`Microsoft.EntityFrameworkCore.Sqlite`) for zero-setup, cross-platform local development and automated testing. No external SQL Server instance is required to run the application.
- **Zero External Infrastructure**: The CRM integration is completely simulated in-process; no external cloud credentials or API keys are required.
- **Diagram Tooling**: Interactive HTML diagrams in `docs/architecture/` are generated via Python 3.12+ scripts (`scripts/render.py`).

---

## Testing & QA

### Test Framework & Runner
- **Backend Testing**: **xUnit** (`Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`).
- **Execution**: Run via `dotnet test`.

### Traceability & Verification Priorities
The test suite is structured around the verification identifiers defined in `docs/architecture/model.json`:

1. **Required Business Rule Test Candidates (Spec Part 5 Mandate)**:
   - `VER-APP-001` (Inquiry Defaults & Timestamps): Verifies creating an inquiry sets `Status = New`, sets `CreatedDate == UpdatedDate`, and status changes advance `UpdatedDate`.
   - `VER-CRM-001` (Persistence Survives CRM Failure): Injects a failing `ICrmClient` (throwing `HttpRequestException`) into `InquiryService` and asserts that inquiry creation and database persistence succeed without rolling back.
2. **Domain & API Verifications**:
   - `VER-APP-002`: Verifies free-form status transitions succeed and invalid status values return 400.
   - `VER-API-002`: Verifies missing required fields or invalid email formats return `400 ValidationProblemDetails`.
   - `VER-API-003`: Verifies operations on non-existent IDs return `404 ProblemDetails`.
   - `VER-DATA-001`: Verifies EF Core SQLite persistence round-trip.
   - `VER-CRM-002`: Verifies Polly retry execution on simulated transient CRM errors.

### Mocking & Isolation Patterns
- **CRM Port Seam**: In unit tests, inject a mock or stub `ICrmClient` to simulate failures, delays, or verify call counts without making external network calls.
- **Database Isolation**: Persistence tests should utilize an in-memory SQLite connection (`DataSource=:memory:`) or isolated temporary test database files to test real EF Core mappings without state leakage.
- **Frontend QA**: Frontend behavior is verified through manual walkthroughs (`VER-UI-001`, `VER-SYS-001`) covering list filtering, details view, and status updates. No automated headless browser runner (e.g., Playwright) is required by the project specification.
