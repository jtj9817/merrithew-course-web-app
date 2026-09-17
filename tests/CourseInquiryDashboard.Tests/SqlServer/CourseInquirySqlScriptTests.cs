using System.Globalization;
using Microsoft.Data.SqlClient;

namespace CourseInquiryDashboard.Tests.SqlServer;

/// <summary>
/// IT-SQL-001..008: executes the shipped SQL Server deliverable
/// (<c>database/database.sql</c>) and its exact marker-delimited report queries
/// against a disposable SQL Server database. Requires
/// <c>SQLSERVER_TEST_CONNECTION_STRING</c>; missing or unreachable
/// infrastructure throws <see cref="SqlServerLaneBlockedException"/> so the
/// cases fail as Blocked and are never skipped green
/// (docs/testing/frontend-and-sql-cases.md §2.3 and §8).
/// </summary>
[Trait("Category", "SqlServer")]
public class CourseInquirySqlScriptTests
{
    private static readonly string[] CanonicalStatusNames = ["New", "Contacted", "Pending", "Registered", "Closed"];

    [Fact]
    [Trait("CaseId", "IT-SQL-001")]
    public async Task Script_creates_the_table_with_samples_covering_statuses_and_optional_columns()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();

        await database.ExecuteScriptAsync();

        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'CourseInquiries';"));

        var total = await database.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.CourseInquiries;");
        Assert.True(total >= 5, $"The script must insert at least five synthetic samples; found {total}.");

        var statuses = (await database.QueryAsync("SELECT DISTINCT Status FROM dbo.CourseInquiries;"))
            .Select(row => (string)row["Status"]!)
            .OrderBy(status => status, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["Closed", "Contacted", "New", "Pending", "Registered"], statuses);

        await AssertNullableCoverageAsync(database, "Phone");
        await AssertNullableCoverageAsync(database, "PreferredLocation");
        await AssertNullableCoverageAsync(database, "Message");
    }

    [Fact]
    [Trait("CaseId", "IT-SQL-002")]
    public async Task Table_matches_the_c1_mapping_and_email_is_not_unique()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await database.ExecuteScriptAsync();

        var columns = (await database.QueryAsync(
                "SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS " +
                "WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'CourseInquiries';"))
            .ToDictionary(row => (string)row["COLUMN_NAME"]!, row => row, StringComparer.Ordinal);

        Assert.Equal(11, columns.Count);
        AssertColumn(columns, "Id", "int", null, "NO");
        AssertColumn(columns, "FirstName", "nvarchar", 100, "NO");
        AssertColumn(columns, "LastName", "nvarchar", 100, "NO");
        AssertColumn(columns, "Email", "nvarchar", 254, "NO");
        AssertColumn(columns, "Phone", "nvarchar", 50, "YES");
        AssertColumn(columns, "CourseName", "nvarchar", 200, "NO");
        AssertColumn(columns, "PreferredLocation", "nvarchar", 200, "YES");
        AssertColumn(columns, "Message", "nvarchar", 4000, "YES");
        AssertColumn(columns, "CreatedDate", "datetime2", null, "NO");
        AssertColumn(columns, "UpdatedDate", "datetime2", null, "NO");

        var status = columns["Status"];
        Assert.Equal("nvarchar", (string)status["DATA_TYPE"]!);
        Assert.Equal("NO", (string)status["IS_NULLABLE"]!);
        Assert.True(
            Convert.ToInt32(status["CHARACTER_MAXIMUM_LENGTH"], CultureInfo.InvariantCulture) >= 10,
            "Status must be nvarchar(>=10).");

        // Id is int IDENTITY(1,1) with the primary key.
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS tc " +
            "INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS kcu " +
            "  ON kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME AND kcu.TABLE_SCHEMA = tc.CONSTRAINT_SCHEMA " +
            "WHERE tc.TABLE_SCHEMA = N'dbo' AND tc.TABLE_NAME = N'CourseInquiries' " +
            "  AND tc.CONSTRAINT_TYPE = N'PRIMARY KEY' AND kcu.COLUMN_NAME = N'Id';"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COLUMNPROPERTY(OBJECT_ID(N'dbo.CourseInquiries'), N'Id', 'IsIdentity');"));
        Assert.Equal(1m, await database.ScalarAsync<decimal>("SELECT IDENT_SEED(N'dbo.CourseInquiries');"));
        Assert.Equal(1m, await database.ScalarAsync<decimal>("SELECT IDENT_INCR(N'dbo.CourseInquiries');"));

        // Status defaults to the canonical New value.
        await database.ExecuteNonQueryAsync(
            "INSERT INTO dbo.CourseInquiries (FirstName, LastName, Email, CourseName, CreatedDate, UpdatedDate) " +
            "VALUES (N'Default', N'Status', N'default-status@example.com', N'Matwork', @created, @created);",
            ("@created", new DateTime(2026, 9, 10, 9, 30, 0, DateTimeKind.Utc)));
        Assert.Equal("New", await database.ScalarAsync<string>(
            "SELECT Status FROM dbo.CourseInquiries WHERE Email = N'default-status@example.com';"));

        // An explicit Id without IDENTITY_INSERT cannot be inserted.
        var identityError = await Assert.ThrowsAsync<SqlException>(() => database.ExecuteNonQueryAsync(
            "INSERT INTO dbo.CourseInquiries (Id, FirstName, LastName, Email, CourseName, Status, CreatedDate, UpdatedDate) " +
            "VALUES (9999, N'Explicit', N'Identity', N'identity-attempt@example.com', N'Matwork', N'New', @created, @created);",
            ("@created", new DateTime(2026, 9, 10, 9, 35, 0, DateTimeKind.Utc))));
        Assert.Equal(544, identityError.Number);
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.CourseInquiries WHERE Email = N'identity-attempt@example.com';"));

        // Email is not unique: a second row with an identical address inserts.
        await database.ExecuteNonQueryAsync(
            "INSERT INTO dbo.CourseInquiries (FirstName, LastName, Email, CourseName, Status, CreatedDate, UpdatedDate) VALUES " +
            "(N'First', N'Duplicate', N'repeat@example.com', N'Reformer', N'New', @created, @created), " +
            "(N'Second', N'Duplicate', N'repeat@example.com', N'Reformer', N'Contacted', @created, @created);",
            ("@created", new DateTime(2026, 9, 10, 9, 40, 0, DateTimeKind.Utc)));
        Assert.Equal(2, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.CourseInquiries WHERE Email = N'repeat@example.com';"));

        // No unique index or constraint covers Email.
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.indexes AS i " +
            "INNER JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id " +
            "INNER JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id " +
            "WHERE i.object_id = OBJECT_ID(N'dbo.CourseInquiries') AND i.is_unique = 1 AND c.name = N'Email';"));
    }

    [Fact]
    [Trait("CaseId", "IT-SQL-003")]
    public async Task Statuses_are_limited_to_the_five_canonical_names()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await database.ExecuteScriptAsync();

        var created = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);
        foreach (var status in CanonicalStatusNames)
        {
            var email = $"status-{status.ToLowerInvariant()}@example.com";
            await database.ExecuteNonQueryAsync(
                "INSERT INTO dbo.CourseInquiries (FirstName, LastName, Email, CourseName, Status, CreatedDate, UpdatedDate) " +
                "VALUES (N'Status', N'Case', @email, N'Matwork', @status, @created, @created);",
                ("@email", email), ("@status", status), ("@created", created));
            Assert.Equal(status, await database.ScalarAsync<string>(
                "SELECT Status FROM dbo.CourseInquiries WHERE Email = @email;", ("@email", email)));
        }

        var unknownError = await Assert.ThrowsAsync<SqlException>(() => database.ExecuteNonQueryAsync(
            "INSERT INTO dbo.CourseInquiries (FirstName, LastName, Email, CourseName, Status, CreatedDate, UpdatedDate) " +
            "VALUES (N'Unknown', N'Status', N'unknown-status@example.com', N'Matwork', N'Unknown', @created, @created);",
            ("@created", created)));
        Assert.Equal(547, unknownError.Number);
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.CourseInquiries WHERE Email = N'unknown-status@example.com';"));
    }

    [Fact]
    [Trait("CaseId", "IT-SQL-004")]
    public async Task Rerunning_the_script_is_idempotent_and_preserves_rows()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await database.ExecuteScriptAsync();

        var countBefore = await database.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.CourseInquiries;");
        var snapshotBefore = await database.SnapshotAsync();
        Assert.True(countBefore >= 5, $"Expected the script samples before the re-run; found {countBefore}.");

        await database.ExecuteScriptAsync();

        Assert.Equal(countBefore, await database.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.CourseInquiries;"));
        Assert.Equal(snapshotBefore, await database.SnapshotAsync());
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'CourseInquiries';"));
    }

    [Fact]
    [Trait("CaseId", "IT-SQL-005")]
    public async Task Last_seven_days_query_is_an_inclusive_rolling_window()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await database.ExecuteScriptAsync();

        // The script samples all predate the fixed window, so the only rows in
        // range are the four inserted here.
        var asOf = new DateTime(2026, 9, 10, 14, 0, 0, DateTimeKind.Utc);
        await InsertInquiryAsync(database, "old@example.com", new DateTime(2026, 9, 2, 13, 59, 59, DateTimeKind.Utc));
        await InsertInquiryAsync(database, "boundary@example.com", new DateTime(2026, 9, 3, 14, 0, 0, DateTimeKind.Utc));
        await InsertInquiryAsync(database, "now@example.com", new DateTime(2026, 9, 10, 14, 0, 0, DateTimeKind.Utc));
        await InsertInquiryAsync(database, "future@example.com", new DateTime(2026, 9, 10, 14, 0, 1, DateTimeKind.Utc));

        var rows = await database.ExecuteQuerySectionAsync("last-7-days", asOf);

        Assert.Equal(2, rows.Count);
        Assert.Equal(
            ["boundary@example.com", "now@example.com"],
            rows.Select(row => (string)row["Email"]!).OrderBy(email => email, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    [Trait("CaseId", "IT-SQL-006")]
    public async Task Count_by_status_reports_only_represented_statuses()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await database.ExecuteScriptAsync();

        // The disposable database was seeded with the script samples; clear it so
        // the reported groups are exactly the distribution under test.
        await database.ExecuteNonQueryAsync("DELETE FROM dbo.CourseInquiries;");

        var created = new DateTime(2026, 9, 10, 11, 0, 0, DateTimeKind.Utc);
        await InsertInquiryAsync(database, "group-new-1@example.com", created, "New");
        await InsertInquiryAsync(database, "group-new-2@example.com", created, "New");
        await InsertInquiryAsync(database, "group-contacted@example.com", created, "Contacted");
        await InsertInquiryAsync(database, "group-pending@example.com", created, "Pending");
        await InsertInquiryAsync(database, "group-registered@example.com", created, "Registered");
        await InsertInquiryAsync(database, "group-closed-1@example.com", created, "Closed");
        await InsertInquiryAsync(database, "group-closed-2@example.com", created, "Closed");

        var counts = await CountByStatusAsync(database);
        Assert.Equal(5, counts.Count);
        Assert.Equal(2, counts["New"]);
        Assert.Equal(1, counts["Contacted"]);
        Assert.Equal(1, counts["Pending"]);
        Assert.Equal(1, counts["Registered"]);
        Assert.Equal(2, counts["Closed"]);

        await database.ExecuteNonQueryAsync(
            "DELETE FROM dbo.CourseInquiries WHERE Email = N'group-new-2@example.com' OR Status = N'Registered';");

        var countsAfterDeletes = await CountByStatusAsync(database);
        Assert.Equal(4, countsAfterDeletes.Count);
        Assert.Equal(1, countsAfterDeletes["New"]);
        Assert.Equal(1, countsAfterDeletes["Contacted"]);
        Assert.Equal(1, countsAfterDeletes["Pending"]);
        Assert.Equal(2, countsAfterDeletes["Closed"]);
        Assert.False(countsAfterDeletes.ContainsKey("Registered"));
    }

    [Fact]
    [Trait("CaseId", "IT-SQL-007")]
    public async Task Duplicate_email_query_normalizes_without_modifying_stored_values()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await database.ExecuteScriptAsync();

        await database.ExecuteNonQueryAsync("DELETE FROM dbo.CourseInquiries;");

        string[] seededEmails =
        [
            "Avery@example.com",
            "  avery@example.com  ",
            "AVERY@EXAMPLE.COM",
            "bia@example.com",
            "Bia@example.com",
            "unique@example.com",
        ];
        var created = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
        foreach (var email in seededEmails)
        {
            await InsertInquiryAsync(database, email, created);
        }

        var groups = (await database.ExecuteQuerySectionAsync("duplicate-email"))
            .ToDictionary(
                row => (string)row["NormalizedEmail"]!,
                row => Convert.ToInt32(row["OccurrenceCount"], CultureInfo.InvariantCulture),
                StringComparer.Ordinal);

        Assert.Equal(2, groups.Count);
        Assert.Equal(3, groups["avery@example.com"]);
        Assert.Equal(2, groups["bia@example.com"]);
        Assert.False(groups.ContainsKey("unique@example.com"));

        // Reporting normalization never rewrites data: casing and whitespace survive.
        var stored = (await database.QueryAsync("SELECT Email FROM dbo.CourseInquiries ORDER BY Id;"))
            .Select(row => (string)row["Email"]!)
            .ToArray();
        Assert.Equal(seededEmails, stored);
    }

    [Fact]
    [Trait("CaseId", "IT-SQL-008")]
    public async Task Date_columns_preserve_datetime2_precision_and_the_utc_instant()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await database.ExecuteScriptAsync();

        var preciseInstant = new DateTime(2026, 9, 10, 14, 0, 0, DateTimeKind.Utc).AddTicks(1_234_567);
        await InsertInquiryAsync(database, "precision@example.com", preciseInstant);

        var dateColumns = (await database.QueryAsync(
                "SELECT COLUMN_NAME, DATA_TYPE, DATETIME_PRECISION FROM INFORMATION_SCHEMA.COLUMNS " +
                "WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'CourseInquiries' " +
                "AND COLUMN_NAME IN (N'CreatedDate', N'UpdatedDate');"))
            .ToDictionary(row => (string)row["COLUMN_NAME"]!, row => row, StringComparer.Ordinal);

        foreach (var columnName in new[] { "CreatedDate", "UpdatedDate" })
        {
            Assert.Equal("datetime2", (string)dateColumns[columnName]["DATA_TYPE"]!);
            Assert.Equal(7, Convert.ToInt32(dateColumns[columnName]["DATETIME_PRECISION"], CultureInfo.InvariantCulture));
        }

        var row = Assert.Single(await database.QueryAsync(
            "SELECT CreatedDate, UpdatedDate FROM dbo.CourseInquiries WHERE Email = N'precision@example.com';"));
        Assert.Equal(preciseInstant, (DateTime)row["CreatedDate"]!);
        Assert.Equal(preciseInstant, (DateTime)row["UpdatedDate"]!);
    }

    private static async Task AssertNullableCoverageAsync(SqlServerTestDatabase database, string column)
    {
        var nullCount = await database.ScalarAsync<int>($"SELECT COUNT(*) FROM dbo.CourseInquiries WHERE [{column}] IS NULL;");
        var valueCount = await database.ScalarAsync<int>($"SELECT COUNT(*) FROM dbo.CourseInquiries WHERE [{column}] IS NOT NULL;");
        Assert.True(
            nullCount >= 1 && valueCount >= 1,
            $"Samples must cover both NULL and non-NULL values for {column} (null={nullCount}, non-null={valueCount}).");
    }

    private static async Task<Dictionary<string, int>> CountByStatusAsync(SqlServerTestDatabase database)
    {
        var rows = await database.ExecuteQuerySectionAsync("count-by-status");
        return rows.ToDictionary(
            row => (string)row["Status"]!,
            row => Convert.ToInt32(row["InquiryCount"], CultureInfo.InvariantCulture),
            StringComparer.Ordinal);
    }

    private static Task<int> InsertInquiryAsync(
        SqlServerTestDatabase database, string email, DateTime createdUtc, string status = "New")
    {
        const string sql =
            "INSERT INTO dbo.CourseInquiries (FirstName, LastName, Email, CourseName, Status, CreatedDate, UpdatedDate) " +
            "VALUES (N'Fixture', N'Inquiry', @email, N'Fixture course', @status, @created, @created);";
        return database.ExecuteNonQueryAsync(sql, ("@email", email), ("@status", status), ("@created", createdUtc));
    }

    private static void AssertColumn(
        IReadOnlyDictionary<string, Dictionary<string, object?>> columns,
        string name,
        string dataType,
        int? characterMaximumLength,
        string isNullable)
    {
        var column = columns[name];
        Assert.Equal(dataType, (string)column["DATA_TYPE"]!);
        Assert.Equal(
            characterMaximumLength,
            column["CHARACTER_MAXIMUM_LENGTH"] is null
                ? (int?)null
                : Convert.ToInt32(column["CHARACTER_MAXIMUM_LENGTH"], CultureInfo.InvariantCulture));
        Assert.Equal(isNullable, (string)column["IS_NULLABLE"]!);
    }
}
