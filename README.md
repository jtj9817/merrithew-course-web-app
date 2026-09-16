# Merrithew Course Inquiry Dashboard

Technical assessment — an ASP.NET Core Web API for managing course registration
inquiries, with a frontend, SQL scripts, and a simulated CRM integration.

> Scaffolding stub. Setup/run instructions, assumptions, "what I'd improve with
> more time", and any AI-tool disclosure will be filled in during implementation.

## Planned stack

- **Backend:** ASP.NET Core Web API (.NET 10)
- **Frontend:** Razor Pages + React + TypeScript
- **Persistence:** SQLite via EF Core (SQL Server-compatible `database.sql` also provided)
- **Tests:** xUnit

## Structure

```text
backend/     ASP.NET Core Web API (Controllers/, Models/, Services/)
frontend/    Razor Pages + React + TypeScript UI
database/    database.sql (schema, sample data, required queries)
tests/       Automated tests
```
