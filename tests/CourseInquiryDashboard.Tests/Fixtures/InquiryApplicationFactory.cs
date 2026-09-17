using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CourseInquiryDashboard.Tests.Fixtures;

public sealed class InquiryApplicationFactory : WebApplicationFactory<Program>
{
    private readonly bool ownsDatabase;
    public string DatabasePath { get; }
    public MutableTimeProvider Clock { get; } = new();
    public string ConnectionString { get; }

    public LogCaptureProvider Logs { get; } = new();
    public ScriptedCrmClient Crm { get; } = new();
    /// <summary>FIX-INTERCEPT hooks applied to the host's DbContext (set before the first client is created).</summary>
    public List<Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor> SaveChangesInterceptors { get; } = [];


    public InquiryApplicationFactory(string? databasePath = null)
    {
        ownsDatabase = databasePath is null;
        DatabasePath = databasePath ?? Path.Combine(Path.GetTempPath(), $"CourseInquiryTests_{Guid.NewGuid():N}.db");
        ConnectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Pooling = false
        }.ToString();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                {
                    DataSource = DatabasePath,
                    Pooling = false
                }.ToString());
                if (SaveChangesInterceptors.Count > 0)
                    options.AddInterceptors(SaveChangesInterceptors);
            });
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<ICrmClient>();
            services.AddSingleton<ICrmClient>(Crm);
            services.AddLogging(logging => logging.AddProvider(Logs));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && ownsDatabase)
            DeleteDatabase();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (ownsDatabase)
            DeleteDatabase();
    }

    private void DeleteDatabase()
    {
        File.Delete(DatabasePath);
        File.Delete(DatabasePath + "-wal");
        File.Delete(DatabasePath + "-shm");
    }
}
