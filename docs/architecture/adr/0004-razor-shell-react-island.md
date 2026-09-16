# 4. Frontend: a Razor Pages shell hosting a React + TypeScript island

Date: 2026-09-16
Status: Accepted

## Context

The spec accepts any frontend from plain HTML to React/TypeScript and states that
"functionality and clarity are more important than design" (Part 3). We want to
demonstrate React + TypeScript skill without taking on a full separate-SPA
hosting and CORS story, and we prefer a single deployable so the app runs with one
command. Microsoft's current React + ASP.NET Core template pattern (checked
2026-09-16) is a clean two-project split built with Vite; we borrow its Vite/TS
tooling but keep one host.

## Decision

ASP.NET Core **Razor Pages render the shell** (layout, the dashboard page). A
**React + TypeScript island**, built with **Vite** into `wwwroot`, mounts into the
dashboard page and provides the interactive list, status filter, detail view, and
status update. The island talks to the same-origin Web API via `fetch`.

## Options considered

### Razor shell + React island (chosen)
- One deployable, one origin (no CORS), one run command.
- Demonstrates both server-rendered ASP.NET and React/TS.
- Server-rendered shell means the page is useful even before the island boots.

### Separate React SPA + Web API (rejected)
- The modern MS pattern; cleanest separation.
- Rejected here: adds a second project, dev-server proxying/CORS, and a second
  thing to run, for marginal benefit at assessment scope.

### Razor Pages only (rejected)
- Simplest and fastest, but does not demonstrate the React/TypeScript skill the
  stack calls for.

### Plain HTML/CSS/JS (rejected)
- Least tooling, least to show.

## Consequences

**Good:** single origin and deployable; shows client and server skills; graceful
shell.

**Bad:** a JavaScript build step (pnpm + Vite) enters the build; the Razor+React
hybrid is less conventional than a clean SPA split, so it needs explaining.

**Watch for:** document the frontend build step in `README.md`; keep the island's
request/response shapes in sync with the API DTOs (ADR-0005).
