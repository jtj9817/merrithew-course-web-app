# 2. Target .NET 10 with ASP.NET Core (Web API + Razor Pages)

Date: 2026-09-16
Status: Accepted

## Context

The assessment requires an ASP.NET Core Web API (spec Part 1). We also chose to
serve a small server-rendered UI from the same app (see ADR-0004), so the host
must support both API controllers and Razor Pages. We want a current, supported
runtime and a single deployable so the run instructions are one command.

Microsoft Learn (checked 2026-09-16) confirms **.NET 10 is the current LTS**,
released November 2025 and supported until **November 2028**. .NET 8 (the prior
LTS) and .NET 9 (STS) both reach end of support in November 2026.

## Decision

We build on **.NET 10 (LTS)** using **ASP.NET Core**, hosting both a **Web API**
(attribute-routed controllers) and **Razor Pages** in one process. We use
**controllers** rather than minimal APIs.

## Options considered

### .NET 10 + controllers (chosen)
- Current LTS; longest support runway.
- Controllers pair naturally with the layered controller → service structure
  (ADR-0005) the code-quality answer defends.

### .NET 8 (rejected)
- Also LTS, widely installed.
- Rejected: reaches end of support Nov 2026, sooner than .NET 10, for no benefit
  here.

### .NET 9 / minimal APIs (rejected)
- .NET 9 is STS (shorter support). Minimal APIs are terser but push routing and
  wiring into `Program.cs`, which muddies the layered story for a CRUD surface
  with DTO validation.

## Consequences

**Good:** modern, supported runtime; one deployable; controllers keep the API
layer tidy and testable.

**Bad:** the reviewer needs the **.NET 10 SDK** installed to build and run.

**Watch for:** state the SDK version prominently in `README.md`. If a grader is
pinned to .NET 8, the code is largely portable, but do not assume it — document
the requirement.
