using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace CourseInquiryDashboard.Tests.SqlServer;

/// <summary>
/// A disposable, uniquely named <c>CourseInquiryTests_&lt;8hex&gt;</c> database on the
/// server named by <c>SQLSERVER_TEST_CONNECTION_STRING</c>. It is created empty,
/// owned by the test run, and dropped on dispose; the supplied catalog (if any)
/// is never opened. All access uses <c>Microsoft.Data.SqlClient</c> directly.
/// </summary>
internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    private readonly string _adminConnectionString;
    private readonly string _databaseConnectionString;
    private bool _disposed;

    private SqlServerTestDatabase(string adminConnectionString, string databaseConnectionString, string name)
    {
        _adminConnectionString = adminConnectionString;
        _databaseConnectionString = databaseConnectionString;
        Name = name;
    }

    internal string Name { get; }

    internal static async Task<SqlServerTestDatabase> CreateAsync()
    {
        var supplied = SqlServerLane.ReadConnectionSettings();
        var admin = SqlServerLane.AdminConnectionSettings(supplied);
        var name = SqlServerLane.NewDatabaseName();
        var databaseSettings = new SqlConnectionStringBuilder(admin.ConnectionString) { InitialCatalog = name };

        try
        {
            await using var connection = new SqlConnection(admin.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{name}];";
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException or IOException)
        {
            throw new SqlServerLaneBlockedException(
                $"Blocked: could not create disposable database '{name}'. Verify that " +
                $"{SqlServerLane.ConnectionStringVariable} points at a running disposable SQL Server: {exception.Message}");
        }

        return new SqlServerTestDatabase(admin.ConnectionString, databaseSettings.ConnectionString, name);
    }

    /// <summary>Executes database/database.sql verbatim as GO-separated batches.</summary>
    internal async Task ExecuteScriptAsync()
    {
        foreach (var batch in SqlServerLane.SplitBatches(SqlServerLane.ReadScript()))
        {
            await ExecuteBatchAsync(batch);
        }
    }

    /// <summary>
    /// Executes the exact text of one <c>-- query: &lt;name&gt;</c> section of
    /// database/database.sql (never a re-implemented copy of the query). When a
    /// fixed UTC <c>@AsOf</c> is supplied it is declared in the same batch, which
    /// the script contract allows (IT-SQL-005).
    /// </summary>
    internal Task<List<Dictionary<string, object?>>> ExecuteQuerySectionAsync(string sectionName, DateTime? asOfUtc = null)
    {
        var section = SqlServerLane.ExtractQuerySection(SqlServerLane.ReadScript(), sectionName);
        return asOfUtc is null
            ? QueryAsync(section)
            : QueryAsync($"DECLARE @AsOf datetime2(7) = @AsOfValue;\n{section}", ("@AsOfValue", asOfUtc.Value));
    }

    /// <summary>
    /// Canonical text snapshot of every row (fixed column order, ordered by Id)
    /// for evidence that activity such as a script re-run changed nothing.
    /// </summary>
    internal async Task<IReadOnlyList<string>> SnapshotAsync()
    {
        const string sql =
            "SELECT Id, FirstName, LastName, Email, Phone, CourseName, PreferredLocation, [Message], Status, " +
            "CreatedDate, UpdatedDate FROM dbo.CourseInquiries ORDER BY Id;";
        await using var connection = await OpenAsync();
        await using var command = CreateCommand(connection, sql, []);
        await using var reader = await command.ExecuteReaderAsync();

        var rows = new List<string>();
        while (await reader.ReadAsync())
        {
            var cells = new string[reader.FieldCount];
            for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
            {
                cells[ordinal] = await reader.IsDBNullAsync(ordinal)
                    ? "<null>"
                    : Describe(reader.GetValue(ordinal));
            }

            rows.Add(string.Join(" | ", cells));
        }

        return rows;
    }

    internal async Task<int> ExecuteNonQueryAsync(string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = await OpenAsync();
        await using var command = CreateCommand(connection, sql, parameters);
        return await command.ExecuteNonQueryAsync();
    }

    internal async Task<T> ScalarAsync<T>(string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = await OpenAsync();
        await using var command = CreateCommand(connection, sql, parameters);
        var value = await command.ExecuteScalarAsync();
        if (value is null || value is DBNull)
        {
            throw new InvalidOperationException($"Expected a non-null scalar result but the query returned NULL. SQL: {sql}");
        }

        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    internal async Task<List<Dictionary<string, object?>>> QueryAsync(string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = await OpenAsync();
        await using var command = CreateCommand(connection, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync();

        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(reader.FieldCount, StringComparer.Ordinal);
            for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
            {
                row[reader.GetName(ordinal)] = await reader.IsDBNullAsync(ordinal) ? null : reader.GetValue(ordinal);
            }

            rows.Add(row);
        }

        return rows;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            using (var pooled = new SqlConnection(_databaseConnectionString))
            {
                SqlConnection.ClearPool(pooled);
            }

            await using var connection = new SqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"IF DB_ID(N'{Name}') IS NOT NULL BEGIN " +
                $"ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Name}]; END;";
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException)
        {
            throw new SqlServerLaneBlockedException(
                $"Blocked: the owned database '{Name}' could not be dropped; the disposable SQL Server lane is " +
                $"not in a trustworthy state: {exception.Message}");
        }
    }

    private static SqlCommand CreateCommand(SqlConnection connection, string sql, (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.Add(value switch
            {
                // AddWithValue would infer the legacy datetime type and lose datetime2(7) precision.
                DateTime dateTime => new SqlParameter(name, SqlDbType.DateTime2) { Value = dateTime, Scale = 7 },
                string text => new SqlParameter(name, SqlDbType.NVarChar) { Value = text },
                int number => new SqlParameter(name, SqlDbType.Int) { Value = number },
                null => new SqlParameter(name, SqlDbType.NVarChar) { Value = DBNull.Value },
                _ => new SqlParameter(name, value),
            });
        }

        return command;
    }

    private async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(_databaseConnectionString);
        try
        {
            await connection.OpenAsync();
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private async Task ExecuteBatchAsync(string batch)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = batch;
        await using var reader = await command.ExecuteReaderAsync();
        do
        {
            while (await reader.ReadAsync())
            {
                // Drain every result set so every statement in the batch executes.
            }
        }
        while (await reader.NextResultAsync());
    }

    private static string Describe(object value) => value switch
    {
        DateTime dateTime => dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "<null>",
    };
}
