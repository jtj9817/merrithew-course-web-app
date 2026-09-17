using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
using System.Text.Json;
using CourseInquiryDashboard.Hosting;
using CourseInquiryDashboard.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CourseInquiryDashboard.Tests.Integration;

/// <summary>
/// IT-OBS cases (OBS-101): submission observability through the full HTTP host —
/// validation-rejection logging with field keys only, the creation audit entry,
/// request-outcome entries, the correlation ID shared across every entry of one
/// request (plus the root-level RFC 7807 <c>traceId</c> extension member on
/// 400/500 ProblemDetails), the
/// <c>/health</c> database readiness probe, and the outcome counters on the
/// <c>CourseInquiryDashboard</c> meter. Privacy (C6) is asserted at the captured
/// sink for every scenario.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ObservabilityApiTests : IAsyncLifetime
{
    private readonly InquiryApplicationFactory factory = new();
    private readonly ConcurrentQueue<(string Instrument, string Outcome, long Value)> measurements = new();
    private HttpClient? client;

    private HttpClient Client => client ??= factory.CreateClient();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        client?.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    [Trait("CaseId", "IT-OBS-001")]
    public async Task Rejected_submission_logs_one_sanitized_validation_entry_with_trace_id()
    {
        var response = await Client.PostAsync("/api/inquiries",
            Json(JsonSerializer.Serialize(SyntheticInquiry.With("email", "not-an-email"))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        // RFC 7807 extension members serialize at the root of the problem object.
        var traceId = problem.RootElement.GetProperty("traceId").GetString();
        Assert.False(string.IsNullOrEmpty(traceId));

        var rejections = factory.Logs.Entries.Where(e => e.EventId.Id == 23).ToArray();
        var rejection = Assert.Single(rejections);
        Assert.Equal(LogLevel.Warning, rejection.Level);
        Assert.Equal("CourseInquiryDashboard.Validation", rejection.Category);
        Assert.Contains("validationRejected", rejection.Message);
        Assert.Contains("Email", StateValue(rejection, "Fields"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(traceId, CorrelationOf(rejection));

        // The same request also produced its terminal outcome entry, same correlation ID.
        Assert.Contains(factory.Logs.Entries, e =>
            e.EventId.Id == 21
            && e.Level == LogLevel.Warning
            && StateValue(e, "Outcome") == "clientError"
            && StateValue(e, "StatusCode") == "400"
            && CorrelationOf(e) == traceId);

        factory.Logs.AssertPrivacy(SyntheticInquiry.Sentinels);
    }

    [Fact]
    [Trait("CaseId", "IT-OBS-002")]
    public async Task Created_submission_logs_audit_entry_sharing_the_correlation_id()
    {
        factory.Crm.Outcomes.Enqueue(_ => Task.FromException(new InvalidOperationException("permanent CRM failure")));

        var response = await Client.PostAsync("/api/inquiries", Json(SyntheticInquiry.Valid()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = document.RootElement.GetProperty("id").GetInt32();

        var correlationIds = factory.Logs.Entries
            .Where(e => CorrelationOf(e) is not null)
            .Select(e => CorrelationOf(e))
            .Distinct()
            .ToList();
        var correlation = Assert.Single(correlationIds);

        var creation = Assert.Single(factory.Logs.Entries, e => e.EventId.Id == 3);
        Assert.Equal(LogLevel.Information, creation.Level);
        Assert.Equal($"Inquiry {id} created", creation.Message);
        Assert.Equal(correlation, CorrelationOf(creation));

        Assert.Contains(factory.Logs.Entries, e =>
            e.EventId.Id == 2 // isolated CRM failure warning
            && StateValue(e, "Outcome") == "failed"
            && CorrelationOf(e) == correlation);
        Assert.Contains(factory.Logs.Entries, e =>
            e.EventId.Id == 20
            && StateValue(e, "Outcome") == "succeeded"
            && StateValue(e, "StatusCode") == "201"
            && CorrelationOf(e) == correlation);

        // REQ-SYS-003 regression guard: the row survived the CRM failure.
        using var fetched = await Client.GetAsync($"/api/inquiries/{id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);

        factory.Logs.AssertPrivacy(SyntheticInquiry.Sentinels);
    }

    [Fact]
    [Trait("CaseId", "IT-OBS-003")]
    public async Task Server_error_carries_trace_id_and_one_correlation_across_all_entries()
    {
        await using var probe = new SqliteConnection(factory.ConnectionString);
        await probe.OpenAsync();
        var drop = probe.CreateCommand();
        drop.CommandText = "DROP TABLE IF EXISTS \"CourseInquiries\"";
        factory.SaveChangesInterceptors.Add(new SaveChangesFaultInjector
        {
            DuringSavingChangesAsync = async (_, _) => await drop.ExecuteNonQueryAsync(),
        });

        var listener = StartMetricsListener(factory.Services.GetRequiredService<InquiryMetrics>());
        try
        {
            var response = await Client.PostAsync("/api/inquiries", Json(SyntheticInquiry.Valid()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var traceId = problem.RootElement.GetProperty("traceId").GetString();
            Assert.False(string.IsNullOrEmpty(traceId));

            Assert.Contains(factory.Logs.Entries, e =>
                e.Message.Contains("Unhandled exception of type", StringComparison.Ordinal)
                && CorrelationOf(e) == traceId);
            Assert.Contains(factory.Logs.Entries, e =>
                e.EventId.Id == 22
                && e.Level == LogLevel.Error
                && StateValue(e, "Outcome") == "serverError"
                && StateValue(e, "StatusCode") == "500"
                && CorrelationOf(e) == traceId);

            factory.Logs.AssertPrivacy(SyntheticInquiry.Sentinels);
            AssertCounter("intake_requests", "serverError", 1L);
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    [Trait("CaseId", "IT-OBS-004")]
    public async Task Health_endpoint_reports_database_readiness()
    {
        var healthy = await Client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, healthy.StatusCode);
        Assert.Equal("Healthy", await healthy.Content.ReadAsStringAsync());

        // Make the database unreachable at runtime (after startup migrations) by
        // shadowing the file with a directory: SQLite cannot open it.
        var moved = factory.DatabasePath + ".held";
        File.Move(factory.DatabasePath, moved);
        Directory.CreateDirectory(factory.DatabasePath);
        try
        {
            var unhealthy = await Client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, unhealthy.StatusCode);
        }
        finally
        {
            Directory.Delete(factory.DatabasePath);
            File.Move(moved, factory.DatabasePath);
        }
    }

    [Fact]
    [Trait("CaseId", "IT-OBS-005")]
    public async Task Outcome_counters_increment_for_intake_and_crm()
    {
        Assert.NotNull(Client); // host started
        // Only this factory's meter instance: parallel test classes run their own
        // hosts with identically-named meters, and exact counts must not see them.
        var metrics = factory.Services.GetRequiredService<InquiryMetrics>();
        var listener = StartMetricsListener(metrics);
        try
        {
            using (var created = await Client.PostAsync("/api/inquiries", Json(SyntheticInquiry.Valid())))
            {
                Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            }
            factory.Crm.Outcomes.Enqueue(_ => Task.FromException(new InvalidOperationException("CRM down")));
            using (var second = await Client.PostAsync("/api/inquiries", Json(SyntheticInquiry.Valid())))
            {
                Assert.Equal(HttpStatusCode.Created, second.StatusCode);
            }
            using (var rejected = await Client.PostAsync("/api/inquiries",
                Json("""{ "lastName": "O'Brien", "email": "a@b.cc", "courseName": "x" }""")))
            {
                Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            }

            AssertCounter("intake_requests", "created", 2L);
            AssertCounter("intake_requests", "validationRejected", 1L);
            AssertCounter("crm_sync_outcomes", "succeeded", 1L);
            AssertCounter("crm_sync_outcomes", "failed", 1L);
        }
        finally
        {
            listener.Dispose();
        }
    }

    private MeterListener StartMetricsListener(InquiryMetrics? ownedBy = null)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == InquiryMetrics.MeterName
                    && (ownedBy is null || ReferenceEquals(instrument.Meter, ownedBy.IntakeOutcomes.Meter)))
                    l.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var outcome = "(none)";
            foreach (var pair in tags)
                if (pair.Key == "outcome")
                    outcome = pair.Value?.ToString() ?? outcome;
            measurements.Enqueue((instrument.Name, outcome, value));
        });
        listener.Start();
        return listener;
    }

    private void AssertCounter(string instrument, string outcome, long expected) =>
        Assert.Equal(expected, measurements
            .Where(m => m.Instrument == instrument && m.Outcome == outcome)
            .Sum(m => m.Value));

    private static string? StateValue(CapturedLog entry, string key) =>
        entry.State.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            .Value?.ToString();

    private static string? CorrelationOf(CapturedLog entry) =>
        entry.Scopes
            .SelectMany(scope => scope is KeyValuePair<string, object?>[] pairs
                ? pairs
                : Enumerable.Empty<KeyValuePair<string, object?>>())
            .FirstOrDefault(pair => pair.Key == "correlationId")
            .Value?.ToString();

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static StringContent Json(object payload) =>
        Json(JsonSerializer.Serialize(payload, WebJson));
}
