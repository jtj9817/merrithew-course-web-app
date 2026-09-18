-- ============================================================================
-- CourseInquiryDashboard - SQLite reconciliation reports (troubleshooting demo)
--
-- Unlike database/database.sql (a SQL Server companion deliverable, C8), this
-- file targets the LIVE application runtime database: EF Core + SQLite
-- (ADR-0003). It ports the same three reports plus a direct by-email lookup to
-- the SQLite dialect, so the "missing inquiry" runbook is reproducible against
-- the running store.
--
-- Run it against the runtime database (default backend/inquiries.db, overridable
-- by ConnectionStrings__DefaultConnection):
--
--     sqlite3 backend/inquiries.db < database/reconciliation.sqlite.sql
--
-- All four reports are read-only: no statement creates, drops, updates, or
-- deletes any row. They mirror the /api/dev/reconciliation endpoint (LINQ over
-- the same store) so the endpoint JSON, this file, and the dashboard readout all
-- agree.
--
-- Date format note
--   CreatedDate/UpdatedDate are stored by EF Core as ISO-8601 TEXT in UTC (via
--   the AppDbContext UtcDateTimeConverter). julianday() parses that text and
--   compares numerically, which avoids the fractional-seconds edge cases a plain
--   lexicographic text comparison at the window boundary would hit.
-- ============================================================================


.headers on
.mode column


-- Report: count-by-status --------------------------------------------------
-- Reconcile the staff-visible totals. Only represented statuses produce a row
-- here; a status the dashboard filter shows as empty simply has no group (the
-- /api/dev/reconciliation endpoint fills the missing statuses in with 0).
SELECT Status, COUNT(*) AS InquiryCount
FROM CourseInquiries
GROUP BY Status
ORDER BY InquiryCount DESC, Status;


-- Report: last-7-days ------------------------------------------------------
-- Rolling seven-day window ending at "now", inclusive; pass a fixed instant
-- instead of 'now' to reproduce a historical report.
SELECT Id, Status, Email, CreatedDate
FROM CourseInquiries
WHERE julianday(CreatedDate) >= julianday('now', '-7 days')
  AND julianday(CreatedDate) <= julianday('now')
ORDER BY CreatedDate DESC, Id DESC;


-- Report: duplicate-email --------------------------------------------------
-- Near-duplicate submissions from visitor resubmits (intake is not idempotent,
-- C5). LOWER() in SQLite is ASCII-only, which is sufficient for the synthetic,
-- local-only data and is noted here deliberately.
SELECT LOWER(TRIM(Email)) AS NormalizedEmail, COUNT(*) AS OccurrenceCount
FROM CourseInquiries
GROUP BY LOWER(TRIM(Email))
HAVING COUNT(*) > 1
ORDER BY OccurrenceCount DESC, NormalizedEmail;


-- Report: by-email ---------------------------------------------------------
-- The stored-but-hidden vs never-stored tiebreaker for one reported email.
-- Replace the literal with the address the visitor reported. A hit under a
-- status the staff view was filtering out (or on a page it was not on) is
-- stored-but-hidden; no hit, plus a validationRejected/serverError log entry
-- for that time, is never-stored.
SELECT Id, Status, CreatedDate, UpdatedDate
FROM CourseInquiries
WHERE Email = 'hannah.becker@example.com' COLLATE NOCASE
ORDER BY CreatedDate DESC, Id DESC;
