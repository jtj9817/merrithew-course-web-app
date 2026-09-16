# 5. Layered controller → service → EF Core, with DTOs and DataAnnotations

Date: 2026-09-16
Status: Accepted

## Context

Part 5 asks us to explain how the solution is organized and why, so the layering
is itself a graded artifact. We want business rules in one testable place (the
required automated test targets a business rule), a clean API contract that does
not expose the entity directly, and validation that returns clear errors — without
over-engineering a small CRUD app.

## Decision

- **Thin controllers** bind and validate DTOs and delegate to a service.
- An **`InquiryService`** owns the business rules (default status, timestamps,
  hard delete, filtering/paging, CRM sync orchestration) and talks to
  `AppDbContext`.
- **Request/response DTOs** (`CreateInquiryDto`, `UpdateStatusDto`,
  `InquiryResponse`) separate the API contract from the EF entity.
- **Validation via DataAnnotations** on the DTOs; invalid input becomes a 400
  ProblemDetails at model binding.

## Options considered

### Service + DTOs (chosen)
- One home for rules; controllers stay trivial; the service is unit-testable
  without HTTP.
- DTOs prevent over-posting and decouple the wire format from the schema.

### Add a repository layer (rejected)
- Controller → Service → Repository → DbContext.
- Rejected: EF Core's `DbContext`/`DbSet` is already a Unit-of-Work + repository;
  another layer adds indirection and mapping for no benefit at this size.

### Controller → DbContext directly (rejected)
- Fewest files, but business logic leaks into controllers, HTTP and rules tangle,
  and it is harder to test — a weaker code-quality story.

### FluentValidation instead of DataAnnotations (rejected)
- More powerful and independently testable, but an extra dependency and wiring;
  DataAnnotations covers required-field, email-format, and known-enum checks
  cleanly.

## Consequences

**Good:** rules are centralized and testable; DTOs give a stable, safe API
surface; controllers are easy to read.

**Bad:** a little DTO↔entity mapping code; DataAnnotations rules live on the DTOs
rather than in a dedicated validator layer.

**Watch for:** if validation grows conditional or cross-field, revisit
FluentValidation. Keep DTOs and the React island's types aligned (ADR-0004).
