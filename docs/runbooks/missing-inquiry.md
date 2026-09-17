# Runbook: "An inquiry is missing from the dashboard"

Operationalizes the Troubleshooting answer in
[written-answers.md](../../written-answers.md) against this system as shipped
(OBS-101 GAP-6). Every command below is runnable against the real runtime:
SQLite for the database, in-app structured logs for the submission path,
`/health` for readiness.

Symptom: staff report that an inquiry a visitor says they submitted does not
appear in the admin list.

## 0. Key facts that narrow this fast

- The inquiry is committed to SQLite **before** CRM sync, and a CRM failure can
  never prevent or undo storage (C5). CRM problems are almost never the cause
  of a *missing* inquiry.
- A rejected submission (400 validation) is **never stored** — but since OBS-101
  it *is* logged (`validationRejected`, EventId 23).
- Every `/api/inquiries` request produces exactly one terminal outcome entry
  (EventIds 20–22), and every log entry of one request shares that request's
  **correlation ID** (`HttpContext.TraceIdentifier`).

## 1. Decision tree: stored-but-hidden vs never-stored vs wrong-store

1. **Stored but hidden (most common).** A status filter is active or the row is
   on another page. The dashboard shows a visible indicator while a filter is
   active ("*Showing N of M inquiries · filtered by X*" with a **Clear
   filter** control). Confirm with the count-by-status query below: a large
   `New` count under an active filter *is* the "bug".
2. **Never stored — validation rejected.** The `POST` returned 400. Confirm in
   logs: `grep -F 'validationRejected'` — the entry names the failing **field
   keys** (never the attempted values) and carries the correlation ID.
3. **Never stored — server error.** The `POST` returned 5xx. Confirm with
   `grep -F 'serverError'` (request-outcome entry) and the sanitized
   `Unhandled exception of type {ErrorType}` entry from the error middleware.
4. **Stored somewhere else.** The list reads the database named by
   `ConnectionStrings__DefaultConnection`. A wrong connection string (or a
   different `inquiries.db` per environment) makes rows "vanish". Startup is
   fatal if migrations did not apply, so a running app always has a migrated
   schema — check *which* file it migrated: confirm the path in the
   environment/configuration, then run the queries below against that same
   file.

## 2. Health check

```bash
curl -i http://localhost:5000/health
```

`200 Healthy` = process alive and the configured SQLite database answers.
`503 Unhealthy` = database unreachable (wrong path, deleted/locked file,
permissions). Fix the store before interpreting anything else.

## 3. Log queries

Logs are structured `ILogger` output (console sink by default). All entries for
one request share a scope `correlationId` equal to the response's `traceId`
problem-details extension — a staff report ("visitor X submitted at 2pm, got an
error page") can be tied to its request via that identifier.

> Scope visibility depends on the sink: the default *simple* console formatter
> does not print scopes, so correlate by the `traceId` value only after enabling
> scope rendering (`IncludeScopes` / JSON console formatter / OTel export) or in
> any structured sink.

- **By correlation ID** (from a staff-reported error response or access log; in
  a scope-rendering sink):

  ```bash
  grep -F '0HN4G2KQHMON3' app.log        # the traceId/correlationId value
  ```

- **By outcome category**:

  ```bash
  grep -E 'validationRejected|clientError|serverError' app.log
  ```

- **By logger/EventId**:

  | Category | EventId | Meaning |
  | --- | ---: | --- |
  | `CourseInquiryDashboard.RequestOutcome` | 20 / 21 / 22 | request completed 2xx (Info) / 4xx (Warn) / 5xx (Error) |
  | `CourseInquiryDashboard.Validation` | 23 | request rejected by validation; failing field keys only |
  | `CourseInquiryDashboard.Services.InquiryService` | 3 | `Inquiry {InquiryId} created` — the commit happened |
  | `CourseInquiryDashboard.Services.InquiryService` | 1 / 2 | CRM outcome isolated after commit (row unaffected) |
  | `CourseInquiryDashboard.Services.SimulatedCrmClient` | 10–14 | CRM attempts and final sync outcome |

  ```bash
  grep -E 'CourseInquiryDashboard\.(RequestOutcome|Validation|Services\.InquiryService)' app.log
  ```

- **Reconcile submissions vs stored rows**: count EventId 20 entries for
  `POST api/inquiries` (status 201) and compare with the count-by-status query
  below; each 201 also logged EventId 3 with the inquiry ID.

No log entry ever contains visitor field values (C6): entries carry IDs,
field *names*, routes, and status codes only.

## 4. Database queries (SQLite dialect)

The runtime database is SQLite (default `backend/inquiries.db`, overridable by
`ConnectionStrings__DefaultConnection`). These are SQLite equivalents of the
report queries shipped in [`database/database.sql`](../../database/database.sql)
(SQL Server dialect, C8):

```bash
sqlite3 /path/to/inquiries.db
```

1. **Count by status** — reconcile staff-visible totals:

   ```sql
   SELECT Status, COUNT(*) AS InquiryCount
   FROM CourseInquiries
   GROUP BY Status
   ORDER BY InquiryCount DESC, Status;
   ```

2. **Last 7 days** — rolling window ending at "now", inclusive; pass a fixed
   instant instead of `'now'` to reproduce a report:

   ```sql
   SELECT Id, Status, Email, CreatedDate
   FROM CourseInquiries
   WHERE julianday(CreatedDate) >= julianday('now', '-7 days')
     AND julianday(CreatedDate) <= julianday('now')
   ORDER BY CreatedDate DESC;
   ```

   (`CreatedDate` is EF Core TEXT UTC; `julianday()` parses it and compares
   numerically, avoiding string-format edge cases at the window boundary.)

3. **Duplicate email** — near-duplicates from visitor resubmits (intake is not
   idempotent, C5). `LOWER()` in SQLite is ASCII-only; sufficient for the
   synthetic/local domain, noted here deliberately:

   ```sql
   SELECT LOWER(TRIM(Email)) AS NormalizedEmail, COUNT(*) AS Copies
   FROM CourseInquiries
   GROUP BY LOWER(TRIM(Email))
   HAVING COUNT(*) > 1
   ORDER BY Copies DESC;
   ```

4. **Direct lookup by the reported email** — the stored-but-hidden vs
   never-stored tiebreaker:

   ```sql
   SELECT Id, Status, CreatedDate, UpdatedDate
   FROM CourseInquiries
   WHERE Email = 'visitor@example.com' COLLATE NOCASE;
   ```

   A hit with `Status <> New-filter` (or a page the staff view was not on) is
   stored-but-hidden; no hit plus a `validationRejected`/`serverError` log
   entry for the reported time is never-stored; no hit and no log entry means
   the request never reached this instance (check which store, and upstream).

## 5. Prevention signals for monitoring

- `GET /health` (liveness + database readiness; 503 when the store is down).
- .NET meter `CourseInquiryDashboard` counters: `intake_requests` (tag
  `outcome` = `created` | `validationRejected` | `serverError`),
  `crm_sync_outcomes` (`succeeded` | `failed` | `timedOut` | `cancelled`),
  `crm_sync_retries`. Alert on error-rate ratios, not raw counts.

## 6. Communicating to stakeholders

Lead with impact: "we've confirmed N inquiries from the last 7 days are stored
and none are lost; they were hidden by a status filter" versus "we're still
confirming whether they reached our system." State what is confirmed, what is
open, and the next update time.
