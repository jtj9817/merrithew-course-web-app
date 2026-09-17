using System.Diagnostics.Metrics;
using CourseInquiryDashboard.Hosting;
using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Models.Dtos;
using CourseInquiryDashboard.Services;
using CourseInquiryDashboard.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CourseInquiryDashboard.Tests.Integration;

/// <summary>
/// IT-APP cases: <see cref="InquiryService"/> over real EF Core/SQLite with the
/// controlled clock (FIX-TIME), scripted CRM (FIX-CRM), real mid-save failure
/// injection (FIX-INTERCEPT), and independent probe connections (FIX-PROBE) on a
/// per-test temp-file database (FIX-DB).
/// </summary>
[Trait("Category", "Integration")]
public sealed class InquiryServiceTests
{
    private static readonly DateTime T0 = new(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime T5 = T0.AddMinutes(5);

    [Fact]
    [Trait("CaseId", "IT-APP-001")]
    public async Task Create_persists_forced_defaults_and_verbatim_visitor_values()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        await using var db = database.CreateContext();
        var service = CreateService(db);

        var dto = SyntheticInquiry.Valid();
        dto.Phone = "  +64 21 555 0123  ";
        dto.PreferredLocation = "  Auckland CBD  ";
        dto.Message = "Welcome to <b>term 2</b> — data & markup 'quoted'";

        var response = await service.CreateAsync(dto);

        Assert.True(response.Id > 0);
        Assert.Equal(Status.New, response.Status);
        Assert.Equal(T0, response.CreatedDate);
        Assert.Equal(response.CreatedDate, response.UpdatedDate);

        var stored = await ReadFreshAsync(database, response.Id);
        Assert.NotNull(stored);
        Assert.Equal(Status.New, stored.Status);
        Assert.Equal(DateTimeKind.Utc, stored.CreatedDate.Kind);
        Assert.Equal(DateTimeKind.Utc, stored.UpdatedDate.Kind);
        Assert.Equal(T0, stored.CreatedDate);
        Assert.Equal(stored.CreatedDate, stored.UpdatedDate);
        Assert.Equal(SyntheticInquiry.FirstName, stored.FirstName);
        Assert.Equal(SyntheticInquiry.LastName, stored.LastName);
        Assert.Equal(SyntheticInquiry.Email, stored.Email);
        Assert.Equal(dto.Phone, stored.Phone);
        Assert.Equal(SyntheticInquiry.CourseName, stored.CourseName);
        Assert.Equal(dto.PreferredLocation, stored.PreferredLocation);
        Assert.Equal(dto.Message, stored.Message);
    }

    [Fact]
    [Trait("CaseId", "IT-APP-002")]
    public async Task Repeated_identical_submissions_each_create_distinct_rows()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        await using var db = database.CreateContext();
        var service = CreateService(db);
        var dto = SyntheticInquiry.Valid();

        var first = await service.CreateAsync(dto);
        var second = await service.CreateAsync(dto);
        // A caller repeats the same payload after a lost response: still another row.
        var third = await service.CreateAsync(dto);

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(second.Id, third.Id);
        Assert.Equal(3, await CountRowsAsync(database));
    }

    [Fact]
    [Trait("CaseId", "IT-APP-003")]
    public async Task Status_transitions_are_free_form_including_backward_and_skip()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        await using var db = database.CreateContext();
        var clock = new MutableTimeProvider();
        var service = CreateService(db, clock);
        var created = await service.CreateAsync(SyntheticInquiry.Valid());

        clock.UtcNow = T5;

        foreach (var target in new[] { Status.Closed, Status.New, Status.Pending, Status.Registered })
        {
            var updated = await service.UpdateStatusAsync(created.Id, target);

            Assert.NotNull(updated);
            Assert.Equal(target, updated.Status);
            Assert.Equal(T5, updated.UpdatedDate); // injected provider time
            Assert.Equal(T0, updated.CreatedDate); // never changes
        }

        var stored = await ReadFreshAsync(database, created.Id);
        Assert.NotNull(stored);
        Assert.Equal(Status.Registered, stored.Status);
        Assert.Equal(T5, stored.UpdatedDate);
        Assert.Equal(T0, stored.CreatedDate);
    }

    [Fact]
    [Trait("CaseId", "IT-APP-004")]
    public async Task Updating_to_the_current_status_is_a_no_op_that_keeps_updatedDate()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        await using var db = database.CreateContext();
        var clock = new MutableTimeProvider();
        var service = CreateService(db, clock);
        var created = await service.CreateAsync(SyntheticInquiry.Valid());

        clock.UtcNow = T5;
        var updated = await service.UpdateStatusAsync(created.Id, Status.New); // same status

        Assert.NotNull(updated);
        Assert.Equal(Status.New, updated.Status);
        Assert.Equal(T0, updated.UpdatedDate); // unchanged, not T5
        var stored = await ReadFreshAsync(database, created.Id);
        Assert.NotNull(stored);
        Assert.Equal(T0, stored.UpdatedDate);
    }

    [Fact]
    [Trait("CaseId", "IT-APP-005")]
    public async Task Frozen_clock_allows_update_with_equal_timestamps()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        await using var db = database.CreateContext();
        var service = CreateService(db); // clock never advanced
        var created = await service.CreateAsync(SyntheticInquiry.Valid());

        var updated = await service.UpdateStatusAsync(created.Id, Status.Contacted);

        Assert.NotNull(updated);
        Assert.Equal(T0, updated.UpdatedDate);
        Assert.Equal(updated.CreatedDate, updated.UpdatedDate); // equal instants are legal
    }

    [Fact]
    [Trait("CaseId", "IT-APP-006")]
    public async Task Backward_clock_correction_stores_the_earlier_timestamp()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        await using var db = database.CreateContext();
        var clock = new MutableTimeProvider();
        var service = CreateService(db, clock);
        var created = await service.CreateAsync(SyntheticInquiry.Valid());

        clock.UtcNow = T5;
        await service.UpdateStatusAsync(created.Id, Status.Contacted);

        clock.UtcNow = T0; // clock correction backwards
        var updated = await service.UpdateStatusAsync(created.Id, Status.Pending);

        Assert.NotNull(updated);
        Assert.Equal(T0, updated.UpdatedDate); // earlier instant stored, no conflict
        var stored = await ReadFreshAsync(database, created.Id);
        Assert.NotNull(stored);
        Assert.Equal(T0, stored.UpdatedDate);
    }

    [Fact]
    [Trait("CaseId", "IT-APP-007")]
    public async Task Fresh_context_round_trips_all_fields_with_utc_kind()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        await using var db = database.CreateContext();
        var service = CreateService(db);
        var dto = SyntheticInquiry.Valid();
        var created = await service.CreateAsync(dto);

        var stored = await ReadFreshAsync(database, created.Id);

        Assert.NotNull(stored);
        Assert.Equal(created.Id, stored.Id);
        Assert.Equal(dto.FirstName, stored.FirstName);
        Assert.Equal(dto.LastName, stored.LastName);
        Assert.Equal(dto.Email, stored.Email);
        Assert.Equal(dto.Phone, stored.Phone);
        Assert.Equal(dto.CourseName, stored.CourseName);
        Assert.Equal(dto.PreferredLocation, stored.PreferredLocation);
        Assert.Equal(dto.Message, stored.Message);
        Assert.Equal(Status.New, stored.Status);
        Assert.Equal(DateTimeKind.Utc, stored.CreatedDate.Kind);
        Assert.Equal(DateTimeKind.Utc, stored.UpdatedDate.Kind);
        Assert.Equal(T0, stored.CreatedDate);
        Assert.Equal(T0, stored.UpdatedDate);
    }

    [Fact]
    [Trait("CaseId", "IT-APP-008")]
    public async Task List_filters_before_counting_and_sorts_deterministically()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        await SeedAsync(database,
            NewRow(1, Status.New, T0),
            NewRow(2, Status.New, T0),                       // tie on CreatedDate
            NewRow(3, Status.Contacted, T0.AddMinutes(1)),
            NewRow(4, Status.Pending, T0.AddMinutes(2)),
            NewRow(5, Status.Pending, T0.AddMinutes(2)),     // tie on CreatedDate
            NewRow(6, Status.Registered, T0.AddMinutes(3)),
            NewRow(7, Status.Closed, T0.AddMinutes(4)),
            NewRow(8, Status.Closed, T0.AddMinutes(1)));     // tie on CreatedDate
        await using var db = database.CreateContext();
        var service = CreateService(db);

        var pending = await service.ListAsync(new(Status.Pending));
        Assert.Equal(2, pending.TotalCount); // filtered count, not the store total
        Assert.Equal([5, 4], Ids(pending));  // desc: newer first, ties by Id desc

        var pendingAsc = await service.ListAsync(new(Status.Pending, Sort: InquirySort.CreatedDateAsc));
        Assert.Equal(2, pendingAsc.TotalCount);
        Assert.Equal([4, 5], Ids(pendingAsc));

        var allDesc = await service.ListAsync(new());
        Assert.Equal(8, allDesc.TotalCount);
        Assert.Equal([7, 6, 5, 4, 8, 3, 2, 1], Ids(allDesc));

        var allAsc = await service.ListAsync(new(Sort: InquirySort.CreatedDateAsc));
        Assert.Equal([1, 2, 3, 8, 4, 5, 6, 7], Ids(allAsc));
    }

    [Fact]
    [Trait("CaseId", "IT-APP-009")]
    public async Task List_pages_exact_slices_and_beyond_end_pages_stay_200()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        await SeedAsync(database, Enumerable.Range(1, 7)
            .Select(i => NewRow(i, Status.New, T0.AddMinutes(i)))
            .ToArray());
        await using var db = database.CreateContext();
        var service = CreateService(db);

        var page2 = await service.ListAsync(new(Page: 2, PageSize: 3));
        Assert.Equal(7, page2.TotalCount);
        Assert.Equal(2, page2.Page);
        Assert.Equal(3, page2.PageSize);
        Assert.Equal([4, 3, 2], Ids(page2)); // desc: skip 7,6,5 then take 4,3,2

        var beyondEnd = await service.ListAsync(new(Page: 99, PageSize: 3));
        Assert.Empty(beyondEnd.Items);
        Assert.Equal(7, beyondEnd.TotalCount); // filtered total survives an empty page
    }

    [Fact]
    [Trait("CaseId", "IT-APP-010")]
    public async Task Delete_is_permanent_and_second_delete_reports_not_found()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        await SeedAsync(database, NewRow(11, Status.Contacted, T0));
        await using var db = database.CreateContext();
        var service = CreateService(db);

        Assert.True(await service.DeleteAsync(11));
        Assert.Null(await ReadFreshAsync(database, 11));
        Assert.Equal(0, await CountRowsAsync(database)); // no Closed archive row, no flag

        Assert.False(await service.DeleteAsync(11)); // zero rows affected → not found
    }

    [Fact]
    [Trait("CaseId", "IT-APP-011")]
    public async Task Concurrent_stale_writers_use_last_committed_write_wins()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        await SeedAsync(database, NewRow(21, Status.New, T0));

        await using var writerA = database.CreateContext();
        await using var writerB = database.CreateContext();
        var serviceA = CreateService(writerA);
        var serviceB = CreateService(writerB); // B's view is based on a stale read of New

        var first = await serviceA.UpdateStatusAsync(21, Status.Contacted);
        var second = await serviceB.UpdateStatusAsync(21, Status.Pending);

        Assert.NotNull(first);
        Assert.NotNull(second); // no conflict exception, no 409 contract
        var stored = await ReadFreshAsync(database, 21);
        Assert.NotNull(stored);
        Assert.Equal(Status.Pending, stored.Status); // last committed write wins
    }

    [Fact]
    [Trait("CaseId", "IT-APP-012")]
    public async Task Vanished_row_mid_write_maps_to_not_found()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        await SeedAsync(database, NewRow(31, Status.New, T0));
        await using var probe = await database.OpenProbeAsync();

        // Update half: the row is deleted through the independent connection inside
        // SavingChanges, after the service loaded it and staged the update.
        var updateInjector = new SaveChangesFaultInjector
        {
            DuringSavingChangesAsync = (_, _) => ExecuteAsync(probe, "DELETE FROM \"CourseInquiries\" WHERE \"Id\" = 31"),
        };
        await using var updateDb = database.CreateContext(updateInjector);
        var updateService = CreateService(updateDb);

        var updated = await updateService.UpdateStatusAsync(31, Status.Closed);

        Assert.Null(updated); // zero-rows-affected → not found, never success
        Assert.Equal(0L, await ScalarAsync(probe, "SELECT COUNT(*) FROM \"CourseInquiries\" WHERE \"Id\" = 31"));

        // Delete half: the vanished row cannot be deleted; the service reports not
        // found and never recreates or reports a successful write.
        Assert.False(await updateService.DeleteAsync(31));
        Assert.Equal(0L, await ScalarAsync(probe, "SELECT COUNT(*) FROM \"CourseInquiries\""));
    }

    [Fact]
    [Trait("CaseId", "IT-APP-013")]
    public async Task Out_of_range_enum_is_rejected_at_the_service_boundary()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        await SeedAsync(database, NewRow(41, Status.New, T0));
        await using var db = database.CreateContext();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateStatusAsync(41, (Status)99));

        var stored = await ReadFreshAsync(database, 41);
        Assert.NotNull(stored);
        Assert.Equal(Status.New, stored.Status); // nothing changed
        Assert.Equal(T0, stored.UpdatedDate);
        Assert.Equal(1, await CountRowsAsync(database)); // nothing persisted or lost

        // Create has no status input at all: the stored status is always forced.
        var created = await service.CreateAsync(SyntheticInquiry.Valid());
        Assert.Equal(Status.New, created.Status);
    }

    [Fact]
    [Trait("CaseId", "IT-APP-014")]
    public async Task Definite_database_failure_before_commit_never_calls_crm()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        await using var probe = await database.OpenProbeAsync();
        var injector = new SaveChangesFaultInjector
        {
            DuringSavingChangesAsync = (_, _) => ExecuteAsync(probe, "DROP TABLE \"CourseInquiries\""),
        };
        await using var db = database.CreateContext(injector);
        var crm = new ScriptedCrmClient();
        var service = CreateService(db, crm: crm);

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => service.CreateAsync(SyntheticInquiry.Valid()));

        Assert.Empty(crm.Calls); // no CRM attempt before commit
        Assert.Equal(0L, await ScalarAsync(probe,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'CourseInquiries'"));
    }

    [Fact]
    [Trait("CaseId", "IT-APP-015")]
    public async Task Commit_is_visible_through_an_independent_connection_before_crm()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        await using var probe = await database.OpenProbeAsync();
        var crm = new ScriptedCrmClient { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var db = database.CreateContext();
        var service = CreateService(db, crm: crm);

        var create = service.CreateAsync(SyntheticInquiry.Valid());
        await crm.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30)); // CRM attempt started

        // The committed row is readable through the independent connection.
        var visible = await ScalarAsync(probe, "SELECT COUNT(*) FROM \"CourseInquiries\"");
        Assert.Equal(1L, visible);
        Assert.Equal("New", await ScalarTextAsync(probe, "SELECT \"Status\" FROM \"CourseInquiries\""));

        // CRM is awaited in-request, not fire-and-forget: create is still parked.
        Assert.False(create.IsCompleted);

        crm.Gate.TrySetResult();
        var response = await create;

        var call = Assert.Single(crm.Calls);
        Assert.Equal(response.Id, call.Id);
        Assert.Equal(Status.New, call.Status);
        Assert.Equal(T0, call.CreatedDate);
        Assert.Equal(T0, call.UpdatedDate);
    }

    [Fact]
    [Trait("CaseId", "IT-APP-016")]
    public async Task Crm_exhaustion_after_commit_never_escapes_create()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        var crm = new ScriptedCrmClient();
        for (var i = 0; i < 4; i++)
            crm.Outcomes.Enqueue(_ => Task.FromException(new HttpRequestException("transient " + i)));
        var logs = new LogCaptureProvider();
        await using var db = database.CreateContext();
        var service = CreateService(db, crm: crm, logs: logs);

        var response = await service.CreateAsync(SyntheticInquiry.Valid()); // no CRM exception escapes

        Assert.True(response.Id > 0);
        var stored = await ReadFreshAsync(database, response.Id);
        Assert.NotNull(stored); // committed values readable from a fresh context
        // The service sees exactly one ICrmClient call that ends in failure; retries
        // are internal to the CRM client and are proven by the UT-CRM cases.
        Assert.Single(crm.Calls);
        Assert.Contains(logs.Entries, entry =>
            StateValue(entry, "outcome")?.ToString() == "failed"
            && StateValue(entry, "inquiryId")?.ToString() == response.Id.ToString());
    }

    [Fact]
    [Trait("CaseId", "IT-APP-017")]
    public async Task Cancellation_after_commit_stops_crm_and_keeps_the_row()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        var crm = new ScriptedCrmClient { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var logs = new LogCaptureProvider();
        await using var db = database.CreateContext();
        var service = CreateService(db, crm: crm, logs: logs);
        using var cts = new CancellationTokenSource();

        var create = service.CreateAsync(SyntheticInquiry.Valid(), cts.Token);
        await crm.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        cts.Cancel(); // token cancelled while the parked CRM attempt is pending

        var response = await create;

        Assert.True(response.Id > 0); // created outcome returned, never rolled back
        Assert.NotNull(await ReadFreshAsync(database, response.Id));
        Assert.Single(crm.Calls); // never replayed
        Assert.Contains(logs.Entries, entry => StateValue(entry, "outcome")?.ToString() == "cancelled");
    }

    [Fact]
    [Trait("CaseId", "IT-APP-018")]
    public async Task Cancellation_before_the_write_starts_persists_nothing()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        await using var probe = await database.OpenProbeAsync();
        var crm = new ScriptedCrmClient();
        await using var db = database.CreateContext();
        var service = CreateService(db, crm: crm);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CreateAsync(SyntheticInquiry.Valid(), cts.Token));

        Assert.Equal(0L, await ScalarAsync(probe, "SELECT COUNT(*) FROM \"CourseInquiries\""));
        Assert.Empty(crm.Calls);
    }

    [Fact]
    [Trait("CaseId", "IT-APP-019")]
    public async Task Mid_write_cancellation_never_claims_success_without_a_committed_row()
    {
        await using var database = new SqliteInquiryDatabase();
        await database.MigrateAsync();
        await using var probe = await database.OpenProbeAsync();
        var crm = new ScriptedCrmClient();
        using var cts = new CancellationTokenSource();
        var injector = new SaveChangesFaultInjector
        {
            DuringSavingChangesAsync = (_, _) =>
            {
                cts.Cancel(); // token cancelled inside SavingChanges, before commands run
                return Task.CompletedTask;
            },
        };
        await using var db = database.CreateContext(injector);
        var service = CreateService(db, crm: crm);

        try
        {
            var response = await service.CreateAsync(SyntheticInquiry.Valid(), cts.Token);

            // Ambiguous branch resolved as committed: the row must exist and be intact.
            var stored = await ReadFreshAsync(database, response.Id);
            Assert.NotNull(stored);
            Assert.Equal(Status.New, stored.Status);
        }
        catch (OperationCanceledException)
        {
            // Ambiguous branch resolved as aborted: nothing persisted, nothing synced.
            Assert.Equal(0L, await ScalarAsync(probe, "SELECT COUNT(*) FROM \"CourseInquiries\""));
            Assert.Empty(crm.Calls);
        }
    }

    private static readonly InquiryMetrics Metrics = new(new Meter(InquiryMetrics.MeterName));

    private static InquiryService CreateService(
        AppDbContext db,
        MutableTimeProvider? clock = null,
        ScriptedCrmClient? crm = null,
        LogCaptureProvider? logs = null)
    {
        var loggerFactory = logs is null
            ? NullLoggerFactory.Instance
            : LoggerFactory.Create(builder => builder.AddProvider(logs));
        return new InquiryService(db, crm ?? new ScriptedCrmClient(), clock ?? new MutableTimeProvider(),
            Metrics, loggerFactory.CreateLogger<InquiryService>());
    }

    private static CourseInquiry NewRow(int id, Status status, DateTime created) => new()
    {
        Id = id,
        FirstName = "Seed",
        LastName = $"Row-{id}",
        Email = $"seed-{id}@example.com",
        Phone = "+64 21 555 0000",
        CourseName = $"Seeding {id:00}",
        PreferredLocation = "Wellington",
        Message = null,
        Status = status,
        CreatedDate = created,
        UpdatedDate = created,
    };

    private static async Task SeedAsync(SqliteInquiryDatabase database, params CourseInquiry[] rows)
    {
        await using var db = database.CreateContext();
        db.CourseInquiries.AddRange(rows);
        await db.SaveChangesAsync();
    }

    private static async Task<CourseInquiry?> ReadFreshAsync(SqliteInquiryDatabase database, int id)
    {
        await using var db = database.CreateContext();
        return await db.CourseInquiries.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id);
    }

    private static async Task<int> CountRowsAsync(SqliteInquiryDatabase database)
    {
        await using var db = database.CreateContext();
        return await db.CourseInquiries.AsNoTracking().CountAsync();
    }

    private static List<int> Ids(InquiryPageResponse page) =>
        page.Items.Select(item => item.Id).ToList();

    private static object? StateValue(CapturedLog entry, string key) =>
        entry.State.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
    private static Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarAsync(SqliteConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<string> ScalarTextAsync(SqliteConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        return (string)(await command.ExecuteScalarAsync())!;
    }
}
