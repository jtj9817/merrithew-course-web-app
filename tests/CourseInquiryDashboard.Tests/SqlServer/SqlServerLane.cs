using System.Text;
using Microsoft.Data.SqlClient;

namespace CourseInquiryDashboard.Tests.SqlServer;

/// <summary>
/// Environment guard and script access for the SQL Server lane. The lane runs
/// only against databases it creates itself (<c>CourseInquiryTests_&lt;8hex&gt;</c>)
/// on the disposable server named by <c>SQLSERVER_TEST_CONNECTION_STRING</c>;
/// the supplied database is never read or altered. The three report queries are
/// executed as the exact text extracted from <c>database/database.sql</c>; no
/// query logic is re-implemented in the harness. Missing or unusable
/// infrastructure throws <see cref="SqlServerLaneBlockedException"/> (Blocked);
/// a missing script file or missing/empty marker section is a script-contract
/// failure (<see cref="FileNotFoundException"/> /
/// <see cref="InvalidOperationException"/>), never a skip and never Blocked
/// (docs/testing/frontend-and-sql-cases.md §2.3 and §8).
/// </summary>
internal static class SqlServerLane
{
    internal const string ConnectionStringVariable = "SQLSERVER_TEST_CONNECTION_STRING";
    internal const string TestDatabasePrefix = "CourseInquiryTests_";

    internal static SqlConnectionStringBuilder ReadConnectionSettings()
    {
        var raw = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new SqlServerLaneBlockedException(
                $"Blocked: {ConnectionStringVariable} is not set. The SQL Server lane needs a disposable SQL Server " +
                "instance (for example a throwaway local container) and is never skipped or substituted with SQLite.");
        }

        SqlConnectionStringBuilder settings;
        try
        {
            settings = new SqlConnectionStringBuilder(raw);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or KeyNotFoundException)
        {
            throw new SqlServerLaneBlockedException(
                $"Blocked: {ConnectionStringVariable} is not a valid SQL Server connection string " +
                $"(the value is never echoed): {exception.Message}");
        }

        var catalog = settings.InitialCatalog;
        if (!catalog.StartsWith(TestDatabasePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new SqlServerLaneBlockedException(
                $"Blocked: refusing to run with catalog '{catalog}'. The supplied Initial Catalog must start with " +
                $"'{TestDatabasePrefix}' (for example '{TestDatabasePrefix}bootstrap'). The harness still creates and " +
                "drops its own uniquely named database and never opens the supplied one.");
        }

        return settings;
    }

    /// <summary>Admin settings target master; the supplied catalog is never opened.</summary>
    internal static SqlConnectionStringBuilder AdminConnectionSettings(SqlConnectionStringBuilder supplied)
        => new(supplied.ConnectionString) { InitialCatalog = "master" };

    internal static string NewDatabaseName() => TestDatabasePrefix + Guid.NewGuid().ToString("N")[..8];

    internal static string ResolveScriptPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "database", "database.sql");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            "database/database.sql was not found in any ancestor directory of " +
            $"'{AppContext.BaseDirectory}'; run the SQL Server lane from the repository checkout. " +
            "A missing script is a script-contract failure, not blocked infrastructure.",
            "database/database.sql");
    }

    internal static string ReadScript() => NormalizeNewlines(File.ReadAllText(ResolveScriptPath()));

    internal static IEnumerable<string> SplitBatches(string scriptText)
    {
        var batch = new StringBuilder();
        foreach (var line in NormalizeNewlines(scriptText).Split('\n'))
        {
            if (IsBatchSeparator(line))
            {
                if (batch.ToString().Trim().Length > 0)
                {
                    yield return batch.ToString();
                }

                batch.Clear();
                continue;
            }

            batch.Append(line).Append('\n');
        }

        if (batch.ToString().Trim().Length > 0)
        {
            yield return batch.ToString();
        }
    }

    /// <summary>
    /// Returns the exact text of one <c>-- query: &lt;name&gt;</c> section, with the
    /// client-side <c>GO</c> batch separator removed (it is not T-SQL and cannot
    /// be sent to the server). The query body itself is never rewritten.
    /// </summary>
    internal static string ExtractQuerySection(string scriptText, string sectionName)
    {
        var lines = NormalizeNewlines(scriptText).Split('\n');
        var marker = $"-- query: {sectionName}";
        var start = Array.FindIndex(lines, line => line.Trim().Equals(marker, StringComparison.OrdinalIgnoreCase));
        if (start < 0)
        {
            throw new InvalidOperationException(
                $"database/database.sql has no '{marker}' section marker; the script contract " +
                "(docs/testing/frontend-and-sql-cases.md §8.3) is broken.");
        }

        var section = new List<string>();
        for (var index = start + 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.TrimStart().StartsWith("-- query:", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (!IsBatchSeparator(line))
            {
                section.Add(line);
            }
        }

        TrimBlankEdges(section);

        if (section.Count == 0)
        {
            throw new InvalidOperationException(
                $"The '{marker}' section of database/database.sql is empty; the script contract is broken.");
        }

        return string.Join('\n', section);
    }

    internal static string NormalizeNewlines(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    private static bool IsBatchSeparator(string line) => line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase);

    private static void TrimBlankEdges(List<string> lines)
    {
        while (lines.Count > 0 && lines[^1].Trim().Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        while (lines.Count > 0 && lines[0].Trim().Length == 0)
        {
            lines.RemoveAt(0);
        }
    }
}
