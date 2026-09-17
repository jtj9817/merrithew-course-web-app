namespace CourseInquiryDashboard.Tests.SqlServer;

/// <summary>
/// The SQL Server lane cannot produce evidence: the disposable server named by
/// <c>SQLSERVER_TEST_CONNECTION_STRING</c> is unset, invalid, or unreachable.
/// The lane throws this instead of skipping, so a blocked case can never be
/// reported as green (docs/testing/frontend-and-sql-cases.md §2.3 and §8.5).
/// A missing <c>database/database.sql</c> or missing section marker is a script
/// contract failure instead (<see cref="FileNotFoundException"/>,
/// <see cref="InvalidOperationException"/>), never Blocked.
/// </summary>
internal sealed class SqlServerLaneBlockedException(string message) : Exception(message);
