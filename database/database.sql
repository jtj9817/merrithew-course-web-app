-- ============================================================================
-- CourseInquiryDashboard - SQL Server companion deliverable (C8)
--
-- The application runtime uses EF Core + SQLite (ADR-0003); this file is a
-- separate SQL Server deliverable, not the runtime bootstrap script. It must
-- run in an empty disposable database and is safe to re-run against a
-- populated one.
--
-- Contents
--   1. dbo.CourseInquiries DDL matching the C1/C2 mapping
--   2. Six synthetic sample rows covering all five statuses and NULL plus
--      non-NULL optional columns
--   3. Three marker-delimited report queries: last-7-days, count-by-status,
--      duplicate-email
--
-- Execution
--   GO-separated batches: sqlcmd -i database/database.sql, or any client that
--   splits on standalone GO lines. All instants are UTC.
--
-- Re-runnability (C8)
--   Every batch is idempotent: the table is created only when absent, a sample
--   is inserted only when no row with its synthetic email exists, and no
--   statement drops, truncates, or updates existing data. A re-run never
--   duplicates samples and never loses real rows.
--
-- Report time
--   The last-7-days query reads @AsOf, declared in the prefix section below.
--   Running this file verbatim uses SYSUTCDATETIME(). A harness may instead
--   extract one '-- query:' section verbatim and DECLARE its own fixed
--   @AsOf datetime2(7) in the same batch (docs/testing/frontend-and-sql-cases.md,
--   IT-SQL-005). T-SQL variables are batch-scoped, so the default declaration
--   shares the batch with the first report query instead of standing alone.
-- ============================================================================


-- Section: schema ------------------------------------------------------------
-- Id is an int identity primary key. Status is stored as a string restricted
-- to the five canonical C2 names with an explicit case-sensitive collation, so
-- stored values are exactly canonical; DEFAULT N'New' mirrors the domain
-- default. Email is deliberately not unique (duplicate detection is
-- analytical, see the duplicate-email query).
IF OBJECT_ID(N'dbo.CourseInquiries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CourseInquiries
    (
        Id                int           IDENTITY(1, 1) NOT NULL
            CONSTRAINT PK_CourseInquiries PRIMARY KEY,
        FirstName         nvarchar(100) NOT NULL,
        LastName          nvarchar(100) NOT NULL,
        Email             nvarchar(254) NOT NULL,
        Phone             nvarchar(50)  NULL,
        CourseName        nvarchar(200) NOT NULL,
        PreferredLocation nvarchar(200) NULL,
        [Message]         nvarchar(4000) NULL,
        Status            nvarchar(10)  COLLATE Latin1_General_100_BIN2 NOT NULL
            CONSTRAINT DF_CourseInquiries_Status DEFAULT N'New'
            CONSTRAINT CK_CourseInquiries_Status CHECK
                (Status IN (N'New', N'Contacted', N'Pending', N'Registered', N'Closed')),
        CreatedDate       datetime2(7)  NOT NULL,
        UpdatedDate       datetime2(7)  NOT NULL
    );
END;
GO


-- Section: synthetic samples -------------------------------------------------
-- Guarded insert: each sample is added only when no row with its synthetic
-- email exists, so re-running this file never duplicates them. Sample instants
-- deliberately predate the fixed 2026-09-03T14:00Z window used by IT-SQL-005
-- and cover all five statuses plus NULL and non-NULL optional columns.
INSERT INTO dbo.CourseInquiries
    (FirstName, LastName, Email, Phone, CourseName, PreferredLocation, [Message], Status, CreatedDate, UpdatedDate)
SELECT sample.FirstName, sample.LastName, sample.Email, sample.Phone, sample.CourseName,
       sample.PreferredLocation, sample.[Message], sample.Status, sample.CreatedDate, sample.UpdatedDate
FROM (VALUES
    (N'Avery',  N'O''Neill', N'avery.oneill@example.com', N'555-0101', N'Reformer Foundations',
     N'Downtown Studio',  N'Please confirm weekend availability.',     N'New',        N'2026-08-18T09:15:00.0000000', N'2026-08-18T09:15:00.0000000'),
    (N'Bianca', N'Rossi',    N'bianca.rossi@example.com', NULL,        N'Matwork Essentials',
     N'Riverside Studio', N'Interested in evening classes.',            N'Contacted',  N'2026-08-21T16:40:00.0000000', N'2026-08-22T10:05:00.0000000'),
    (N'Chen',   N'Wei',      N'chen.wei@example.com',     NULL,        N'Cadillac Intensive',
     NULL,                NULL,                                         N'Pending',    N'2026-08-24T07:30:00.0000000', N'2026-08-24T07:30:00.0000000'),
    (N'Dalia',  N'Haddad',   N'dalia.haddad@example.com', N'555-0144', N'Prenatal Pilates',
     NULL,                NULL,                                         N'Registered', N'2026-08-27T13:20:00.0000000', N'2026-08-29T11:45:00.0000000'),
    (N'Emil',   N'Novak',    N'emil.novak@example.com',   N'555-0177', N'Matwork Essentials',
     N'Northside Studio', N'Session completed; closing this inquiry.',  N'Closed',     N'2026-08-30T18:10:00.0000000', N'2026-09-01T08:00:00.0000000'),
    (N'Fern',   N'Okafor',   N'fern.okafor@example.com',  NULL,        N'Reformer Foundations',
     N'Downtown Studio',  NULL,                                         N'New',        N'2026-09-02T12:00:00.0000000', N'2026-09-02T12:00:00.0000000')
) AS sample (FirstName, LastName, Email, Phone, CourseName, PreferredLocation, [Message], Status, CreatedDate, UpdatedDate)
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.CourseInquiries AS existing WHERE existing.Email = sample.Email
);
GO


-- Prefix section: report time ------------------------------------------------
-- @AsOf is the single UTC report instant read by the three queries below; the
-- default is the current UTC instant. This DECLARE intentionally stays in the
-- same batch as the first query because T-SQL variables are batch-scoped.
DECLARE @AsOf datetime2(7) = SYSUTCDATETIME();

-- Rolling seven-day window ending at @AsOf, inclusive at both ends; older and
-- future rows are excluded.
-- query: last-7-days
SELECT Id, FirstName, LastName, Email, Phone, CourseName, PreferredLocation, [Message], Status, CreatedDate, UpdatedDate
FROM dbo.CourseInquiries
WHERE CreatedDate >= DATEADD(day, -7, @AsOf)
  AND CreatedDate <= @AsOf
ORDER BY CreatedDate, Id;
GO


-- Groups stored rows by status, including Closed. Only represented statuses
-- produce groups; hard-deleted rows produce none.
-- query: count-by-status
SELECT Status, COUNT(*) AS InquiryCount
FROM dbo.CourseInquiries
GROUP BY Status
ORDER BY Status;
GO


-- Reporting-only duplicate detection. Groups by LOWER(LTRIM(RTRIM(Email)))
-- with Latin1_General_100_BIN2 applied inside LOWER, so both the case mapping
-- and the grouping comparison are independent of the database default
-- collation. Stored values are not modified and email stays non-unique.
-- query: duplicate-email
SELECT LOWER(LTRIM(RTRIM(Email)) COLLATE Latin1_General_100_BIN2) AS NormalizedEmail,
       COUNT(*) AS OccurrenceCount
FROM dbo.CourseInquiries
GROUP BY LOWER(LTRIM(RTRIM(Email)) COLLATE Latin1_General_100_BIN2)
HAVING COUNT(*) > 1
ORDER BY NormalizedEmail;
