# 3. Persist with EF Core + SQLite, ship a SQL Server database.sql, use int keys

Date: 2026-09-16
Status: Accepted

## Context

The spec prefers SQL Server but accepts SQLite, LocalDB, or in-memory "if setup
is clearly documented" (Part 2). Independently, it requires a hand-written
`database.sql` containing a `CourseInquiries` table script, 5+ sample rows, and
three queries (last-7-days, count-by-status, duplicate-by-email). We want a
grader to clone and run with **zero database setup**, on any OS.

## Decision

- **Runtime store:** EF Core **code-first over SQLite**, a single file created
  automatically via migrations on startup.
- **Deliverable script:** a hand-written `database/database.sql` in **SQL Server
  dialect** (the stated preference), with a note on the SQLite differences.
- **Primary key:** **int identity / autoincrement**.

## Options considered

### SQLite via EF Core (chosen)
- No install; file-based; runs on Linux/macOS/Windows identically.
- Exercises real SQL and real EF Core mapping (unlike in-memory).

### SQL Server / LocalDB (rejected)
- The stated preference and closest to production.
- Rejected as the *runtime* store: setup burden on a cross-platform grader;
  LocalDB is Windows-only. We honour the preference in `database.sql` instead.

### In-memory (rejected)
- Zero setup, but data vanishes each run, weakens the demo, and does not prove
  the SQL works.

### Int identity (chosen) vs GUID (rejected)
- Int gives readable sample rows and URLs (`/api/inquiries/5`) and small keys.
- GUID's cross-system uniqueness and non-enumerable URLs are unnecessary for a
  single-node internal tool and make the sample data noisier.

## Consequences

**Good:** clone-and-run; honours the SQL Server preference on paper; real SQL is
demonstrated both ways.

**Bad:** **two schema sources** — EF migrations and `database.sql` — that can
drift; and the two dialects differ (`INT IDENTITY(1,1)` vs
`INTEGER PRIMARY KEY AUTOINCREMENT`, `DATETIME2` vs `TEXT`/`NUMERIC`).

**Watch for:** treat the entity + migration as the source of truth and keep
`database.sql` matching it by hand; call out the dialect differences in the
script's comments and the README.
