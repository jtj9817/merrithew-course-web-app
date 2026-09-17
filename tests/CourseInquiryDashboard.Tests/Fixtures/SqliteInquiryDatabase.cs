using CourseInquiryDashboard.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CourseInquiryDashboard.Tests.Fixtures;

/// <summary>
/// FIX-DB: one isolated SQLite database per test. The schema comes from the real EF
/// migrations, never <c>EnsureCreated</c>. Every case gets a unique temp file (never
/// <c>:memory:</c>) so independent probe connections can observe committed rows while a
/// service is still running, and out-of-band SQL can run mid-save (contracts C5, C8).
/// </summary>
public sealed class SqliteInquiryDatabase : IAsyncDisposable
{
    private readonly List<SqliteConnection> probes = [];
    private bool migrated;

    public string DatabasePath { get; }

    public string ConnectionString { get; }

    public SqliteInquiryDatabase()
    {
        DatabasePath = Path.Combine(Path.GetTempPath(), $"CourseInquiryService_{Guid.NewGuid():N}.db");
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Pooling = false,
        }.ToString();
    }

    /// <summary>A context over the same file, optionally observing injected interceptors.</summary>
    public AppDbContext CreateContext(params Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ConnectionString)
            .AddInterceptors(interceptors)
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Applies the real migrations once; repeat calls are no-ops.</summary>
    public async Task MigrateAsync()
    {
        if (migrated)
            return;
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        migrated = true;
    }

    /// <summary>FIX-PROBE: an independent connection kept open for the test's lifetime.</summary>
    public async Task<SqliteConnection> OpenProbeAsync()
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        probes.Add(connection);
        return connection;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var probe in probes)
            await probe.DisposeAsync();

        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            try
            {
                File.Delete(DatabasePath + suffix);
            }
            catch (IOException)
            {
                // A pooled/still-open handle may delay deletion on some platforms; the
                // file is in the temp dir, so leaving it briefly is harmless.
            }
        }
    }
}
