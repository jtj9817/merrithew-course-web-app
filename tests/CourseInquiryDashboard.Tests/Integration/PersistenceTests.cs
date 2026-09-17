using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CourseInquiryDashboard.Tests.Integration;

/// <summary>
/// IT-DATA cases: the real startup migration path, restart durability, fatal migration failure, raw
/// schema constraints, and UTC persistence/representation. Every case uses its own temporary SQLite
/// file so fresh connections can observe committed state (FIX-DB).
/// </summary>
[Trait("Category", "Integration")]
public sealed class PersistenceTests : IAsyncLifetime
{
    private const string DuplicateEmail = "aiko.obrien+test@example.com";

    private const string InsertInquirySql =
        """
        INSERT INTO "CourseInquiries"
            ("FirstName", "LastName", "Email", "Phone", "CourseName", "PreferredLocation", "Message", "Status", "CreatedDate", "UpdatedDate")
        VALUES
            ($firstName, $lastName, $email, $phone, $courseName, $preferredLocation, $message, $status, $createdDate, $updatedDate)
        """;

    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"CourseInquiryPersistence_{Guid.NewGuid():N}.db");

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        DeleteDatabaseFile();
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("CaseId", "IT-DATA-001")]
    public async Task ItData001_New_database_file_is_migrated_by_host_startup_and_host_then_serves()
    {
        Assert.False(File.Exists(databasePath));

        await using var factory = new InquiryApplicationFactory(databasePath);
        using var httpFactory = WithProbeController(factory);
        using var client = httpFactory.CreateClient();

        Assert.True(File.Exists(databasePath));

        using var ping = await client.GetAsync("test-support/persistence/ping");
        Assert.Equal(HttpStatusCode.OK, ping.StatusCode);

        await using var connection = await OpenProbeAsync();
        Assert.True(await TableExistsAsync(connection, "CourseInquiries"));
        Assert.True(await TableExistsAsync(connection, "__EFMigrationsHistory"));
        Assert.NotEmpty(await AppliedMigrationIdsAsync(connection));
    }

    [Fact]
    [Trait("CaseId", "IT-DATA-001")]
    public async Task Migrations_create_inquiry_table()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

        await db.Database.MigrateAsync();

        Assert.Equal(1L, await ScalarLongAsync(
            connection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'CourseInquiries'"));
        Assert.True(await ScalarLongAsync(connection, "SELECT COUNT(*) FROM \"__EFMigrationsHistory\"") >= 1);
    }

    [Fact]
    [Trait("CaseId", "IT-DATA-002")]
    public async Task ItData002_Repeat_startup_on_the_same_file_preserves_rows_and_duplicate_emails()
    {
        List<int> seededIds;

        await using (var first = new InquiryApplicationFactory(databasePath))
        {
            using var client = first.CreateClient();
            using var scope = first.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = first.Clock.UtcNow;

            db.CourseInquiries.Add(NewInquiry(DuplicateEmail, Status.New, now));
            db.CourseInquiries.Add(NewInquiry(DuplicateEmail, Status.Contacted, now));
            await db.SaveChangesAsync();

            seededIds = await db.CourseInquiries.OrderBy(row => row.Id).Select(row => row.Id).ToListAsync();
            Assert.Equal(2, seededIds.Count);
            Assert.Equal(2, seededIds.Distinct().Count());
        }

        await using (var connection = await OpenProbeAsync())
        {
            Assert.Equal(2L, await ScalarLongAsync(
                connection,
                "SELECT COUNT(*) FROM \"CourseInquiries\" WHERE \"Email\" = $email",
                ("$email", DuplicateEmail)));
        }

        for (var restart = 0; restart < 2; restart++)
        {
            await using var restarted = new InquiryApplicationFactory(databasePath);
            using var client = restarted.CreateClient();

            await using var fresh = CreateFreshContext();
            Assert.Equal(seededIds, await fresh.CourseInquiries.OrderBy(row => row.Id).Select(row => row.Id).ToListAsync());
            Assert.Equal(
                new[] { Status.New, Status.Contacted },
                await fresh.CourseInquiries.OrderBy(row => row.Id).Select(row => row.Status).ToListAsync());
        }
    }

    [Fact]
    [Trait("CaseId", "IT-DATA-003")]
    public async Task ItData003_Failing_migration_appended_after_the_real_set_makes_startup_fatal()
    {
        // Real startup first, so the failing migration is appended on top of a genuine schema.
        await using (var migrated = new InquiryApplicationFactory(databasePath))
        {
            using var client = migrated.CreateClient();
            using var scope = migrated.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CourseInquiries.Add(NewInquiry(DuplicateEmail, Status.New, migrated.Clock.UtcNow));
            await db.SaveChangesAsync();
        }

        IReadOnlyList<string> appliedBefore;
        await using (var connection = await OpenProbeAsync())
        {
            appliedBefore = await AppliedMigrationIdsAsync(connection);
        }

        Assert.NotEmpty(appliedBefore);
        Assert.DoesNotContain(TestOnlyFatalMigration.Id, appliedBefore);

        // The failing host discovers the test-only migration through the migrations assembly option;
        // the production migration set itself is untouched.
        using var failing = new InquiryApplicationFactory(databasePath).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(
                    new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString(),
                    sqlite => sqlite.MigrationsAssembly(typeof(TestOnlyFatalMigration).Assembly.GetName().Name!)));
            }));

        Exception? startupFailure = null;
        try
        {
            _ = failing.CreateClient();
        }
        catch (Exception exception)
        {
            startupFailure = exception;
        }

        Assert.NotNull(startupFailure);
        Assert.Contains(TestOnlyFatalMigration.SentinelMessage, Describe(startupFailure!));

        // Fatal, not a fallback: the real schema and its row are still the ones in the file, the
        // failing migration was never recorded, and a normal host still starts on the same file.
        await using (var connection = await OpenProbeAsync())
        {
            Assert.Equal(appliedBefore, await AppliedMigrationIdsAsync(connection));
            Assert.Equal(1L, await ScalarLongAsync(connection, "SELECT COUNT(*) FROM \"CourseInquiries\""));
        }

        await using (var recovered = new InquiryApplicationFactory(databasePath))
        {
            using var client = recovered.CreateClient();
            await using var fresh = CreateFreshContext();
            Assert.Equal(1, await fresh.CourseInquiries.CountAsync());
        }
    }

    [Fact]
    [Trait("CaseId", "IT-DATA-004")]
    public async Task ItData004_Raw_schema_rejects_invalid_status_and_missing_required_values_but_allows_duplicates()
    {
        await using var factory = new InquiryApplicationFactory(databasePath);
        using var client = factory.CreateClient();

        await using var connection = await OpenProbeAsync();

        var nullability = await ColumnNullabilityAsync(connection);
        foreach (var required in new[] { "FirstName", "LastName", "Email", "CourseName", "Status", "CreatedDate", "UpdatedDate" })
        {
            Assert.True(nullability[required], $"'{required}' should be NOT NULL.");
        }

        foreach (var optional in new[] { "Phone", "PreferredLocation", "Message" })
        {
            Assert.False(nullability[optional], $"'{optional}' should be nullable.");
        }

        // Nullable optional columns and a repeated email are accepted: email is not unique (C8).
        Assert.Equal(1, await InsertInquiryRawAsync(connection));
        Assert.Equal(1, await InsertInquiryRawAsync(connection, status: "Closed", message: "Second submission"));
        Assert.Equal(2L, await ScalarLongAsync(
            connection,
            "SELECT COUNT(*) FROM \"CourseInquiries\" WHERE \"Email\" = $email",
            ("$email", DuplicateEmail)));

        var statusFailure = await Assert.ThrowsAsync<SqliteException>(
            () => InsertInquiryRawAsync(connection, status: "Draft"));
        Assert.Contains(AppDbContext.StatusCheckConstraintName, statusFailure.Message);

        var requiredFailure = await Assert.ThrowsAsync<SqliteException>(
            () => InsertInquiryRawAsync(connection, firstName: null));
        Assert.Contains("FirstName", requiredFailure.Message);

        // HTTP bypassed: an undefined enum cannot persist either.
        await using (var context = CreateFreshContext())
        {
            context.CourseInquiries.Add(NewInquiry(DuplicateEmail, (Status)99, factory.Clock.UtcNow));
            var enumFailure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Contains(AppDbContext.StatusCheckConstraintName, Describe(enumFailure));
        }
    }

    [Fact]
    [Trait("CaseId", "IT-DATA-005")]
    public async Task ItData005_Utc_timestamps_survive_raw_storage_fresh_reads_and_json_without_shifting()
    {
        var t0 = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

        await using var factory = new InquiryApplicationFactory(databasePath);
        factory.Clock.UtcNow = t0;
        using var httpFactory = WithProbeController(factory);
        using var client = httpFactory.CreateClient();

        int id;
        using (var scope = httpFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var inquiry = NewInquiry(DuplicateEmail, Status.Pending, t0);
            db.CourseInquiries.Add(inquiry);
            await db.SaveChangesAsync();
            id = inquiry.Id;
        }

        Assert.True(id > 0);

        await using (var connection = await OpenProbeAsync())
        {
            Assert.Equal(t0.UtcDateTime, DateTime.Parse(
                (await ScalarTextAsync(connection, $"SELECT \"CreatedDate\" FROM \"CourseInquiries\" WHERE \"Id\" = {id}"))!,
                CultureInfo.InvariantCulture));
            Assert.Equal("Pending", await ScalarTextAsync(
                connection,
                $"SELECT \"Status\" FROM \"CourseInquiries\" WHERE \"Id\" = {id}"));
        }

        // A fresh connection reloads every field verbatim and restores Kind to UTC.
        await using (var fresh = CreateFreshContext())
        {
            var reloaded = await fresh.CourseInquiries.AsNoTracking().SingleAsync(row => row.Id == id);

            Assert.Equal("Aiko", reloaded.FirstName);
            Assert.Equal("O'Brien", reloaded.LastName);
            Assert.Equal(DuplicateEmail, reloaded.Email);
            Assert.Equal("+64 21 555 0123", reloaded.Phone);
            Assert.Equal("Patisserie 301 — evening", reloaded.CourseName);
            Assert.Equal("Auckland CBD", reloaded.PreferredLocation);
            Assert.Equal("Welcome to <b>term 2</b>", reloaded.Message);
            Assert.Equal(Status.Pending, reloaded.Status);

            Assert.Equal(DateTimeKind.Utc, reloaded.CreatedDate.Kind);
            Assert.Equal(t0.UtcDateTime, reloaded.CreatedDate);
            Assert.Equal(DateTimeKind.Utc, reloaded.UpdatedDate.Kind);
            Assert.Equal(t0.UtcDateTime, reloaded.UpdatedDate);
        }

        // HTTP JSON keeps the UTC designator without shifting the instant.
        using var response = await client.GetAsync($"test-support/persistence/inquiries/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var createdDate = document.RootElement.GetProperty("createdDate").GetString();
        var updatedDate = document.RootElement.GetProperty("updatedDate").GetString();

        Assert.NotNull(createdDate);
        Assert.NotNull(updatedDate);
        Assert.EndsWith("Z", createdDate!);
        Assert.EndsWith("Z", updatedDate!);

        var parsedCreated = DateTime.Parse(createdDate!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        Assert.Equal(DateTimeKind.Utc, parsedCreated.Kind);
        Assert.Equal(t0.UtcDateTime, parsedCreated);
        Assert.Equal(t0.UtcDateTime, DateTime.Parse(updatedDate!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    private string ProbeConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Pooling = false,
    }.ToString();

    private static WebApplicationFactory<Program> WithProbeController(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(PersistenceProbeController).Assembly)));

    private AppDbContext CreateFreshContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(ProbeConnectionString).Options);

    private async Task<SqliteConnection> OpenProbeAsync()
    {
        var connection = new SqliteConnection(ProbeConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static CourseInquiry NewInquiry(string email, Status status, DateTimeOffset now) => new()
    {
        FirstName = "Aiko",
        LastName = "O'Brien",
        Email = email,
        Phone = "+64 21 555 0123",
        CourseName = "Patisserie 301 — evening",
        PreferredLocation = "Auckland CBD",
        Message = "Welcome to <b>term 2</b>",
        Status = status,
        CreatedDate = now.UtcDateTime,
        UpdatedDate = now.UtcDateTime,
    };

    private static async Task<int> InsertInquiryRawAsync(
        SqliteConnection connection,
        string status = "New",
        string? firstName = "Aiko",
        string? lastName = "O'Brien",
        string? email = DuplicateEmail,
        string? phone = null,
        string? preferredLocation = null,
        string? message = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = InsertInquirySql;
        command.Parameters.AddWithValue("$firstName", firstName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$lastName", lastName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$email", email ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$phone", phone ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$courseName", "Patisserie 301 — evening");
        command.Parameters.AddWithValue("$preferredLocation", preferredLocation ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$message", message ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$createdDate", "2026-03-01 10:00:00");
        command.Parameters.AddWithValue("$updatedDate", "2026-03-01 10:00:00");
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName) =>
        await ScalarLongAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name", ("$name", tableName)) == 1L;

    private static async Task<IReadOnlyList<string>> AppliedMigrationIdsAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"";
        var ids = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    private static async Task<IReadOnlyDictionary<string, bool>> ColumnNullabilityAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """PRAGMA table_info("CourseInquiries")""";
        var nullability = new Dictionary<string, bool>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            nullability[reader.GetString(1)] = reader.GetInt32(3) == 1;
        }

        Assert.NotEmpty(nullability);
        return nullability;
    }

    private static async Task<long> ScalarLongAsync(
        SqliteConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        var value = await ExecuteScalarAsync(connection, sql, parameters);
        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static async Task<string?> ScalarTextAsync(
        SqliteConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        var value = await ExecuteScalarAsync(connection, sql, parameters);
        return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static async Task<object?> ExecuteScalarAsync(
        SqliteConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return await command.ExecuteScalarAsync();
    }

    private static string Describe(Exception exception)
    {
        var text = new StringBuilder();

        void Append(Exception current, int depth)
        {
            if (depth > 10)
            {
                return;
            }

            text.AppendLine($"{current.GetType().FullName}: {current.Message}");

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    Append(inner, depth + 1);
                }
            }
            else if (current.InnerException is { } innerException)
            {
                Append(innerException, depth + 1);
            }
        }

        Append(exception, 0);
        return text.ToString();
    }

    private void DeleteDatabaseFile()
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            File.Delete(databasePath + suffix);
        }
    }
}
