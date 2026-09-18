using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
using System.Text.Json;
using CourseInquiryDashboard.Hosting;
using CourseInquiryDashboard.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CourseInquiryDashboard.Tests.Integration;

/// <summary>
/// IT-FAULT and IT-RECON cases: the two troubleshooting-demonstration dev surfaces
/// through the full HTTP host. The intake fault switch stages a genuine 500 that
/// stores no row (with the sanitized ErrorType log, serverError metric, and traceId
/// OBS-101 already provides); the read-only reconciliation reports count-by-status,
/// the seven-day window, duplicate emails, and by-email lookups over the live SQLite
/// store. Both are absent outside Development. Privacy (C6) is asserted at the sink.
/// </summary>
[Trait("Category", "Integration")]
public sealed class IntakeFaultAndReconciliationApiTests : IAsyncLifetime
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

    // ── Intake fault switch ────────────────────────────────────────────────

    [Fact]
    [Trait("CaseId", "IT-FAULT-001")]
    public async Task Fault_switch_reports_inert_state_initially()
    {
        using var response = await Client.GetAsync("/api/dev/intake-fault");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        Assert.Equal(0, document.RootElement.GetProperty("armed").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("totalInjected").GetInt64());
    }

    [Fact]
    [Trait("CaseId", "IT-FAULT-002")]
    public async Task Arm_round_trips_and_get_reflects_it()
    {
        using var armed = await Client.PutAsync("/api/dev/intake-fault", Json("""{ "armCount": 2 }"""));
        Assert.Equal(HttpStatusCode.OK, armed.StatusCode);
        using var armedDocument = await ReadJsonAsync(armed);
        Assert.Equal(2, armedDocument.RootElement.GetProperty("armed").GetInt32());

        using var read = await Client.GetAsync("/api/dev/intake-fault");
        using var readDocument = await ReadJsonAsync(read);
        Assert.Equal(2, readDocument.RootElement.GetProperty("armed").GetInt32());
    }

    [Fact]
    [Trait("CaseId", "IT-FAULT-003")]
    public async Task Arm_rejects_out_of_range_or_missing_values_with_problem_json()
    {
        foreach (var payload in new[] { """{ "armCount": -1 }""", """{ "armCount": 101 }""", "{ }" })
        {
            using var response = await Client.PutAsync("/api/dev/intake-fault", Json(payload));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }

        // The switch stays inert after every rejected arm.
        using var state = await Client.GetAsync("/api/dev/intake-fault");
        using var document = await ReadJsonAsync(state);
        Assert.Equal(0, document.RootElement.GetProperty("armed").GetInt32());
    }

    [Fact]
    [Trait("CaseId", "IT-FAULT-004")]
    public async Task Armed_submission_returns_500_stores_no_row_and_is_observable()
    {
        using var armed = await Client.PutAsync("/api/dev/intake-fault", Json("""{ "armCount": 1 }"""));
        Assert.Equal(HttpStatusCode.OK, armed.StatusCode);

        var listener = StartMetricsListener(factory.Services.GetRequiredService<InquiryMetrics>());
        try
        {
            using var failed = await Client.PostAsync("/api/inquiries", Json(SyntheticInquiry.Valid()));

            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            using var problem = JsonDocument.Parse(await failed.Content.ReadAsStringAsync());
            var traceId = problem.RootElement.GetProperty("traceId").GetString();
            Assert.False(string.IsNullOrEmpty(traceId));

            // The sanitized error entry names the injected exception type, not visitor data.
            Assert.Contains(factory.Logs.Entries, entry =>
                entry.Message.Contains("Unhandled exception of type", StringComparison.Ordinal)
                && (StateValue(entry, "ErrorType") ?? string.Empty).Contains(
                    nameof(CourseInquiryDashboard.DevTools.IntakeFaultInjectedException), StringComparison.Ordinal)
                && CorrelationOf(entry) == traceId);

            // The terminal request-outcome entry records the 500, same correlation ID.
            Assert.Contains(factory.Logs.Entries, entry =>
                entry.EventId.Id == 22
                && StateValue(entry, "Outcome") == "serverError"
                && StateValue(entry, "StatusCode") == "500"
                && CorrelationOf(entry) == traceId);

            AssertCounter("intake_requests", "serverError", 1L);
            factory.Logs.AssertPrivacy(SyntheticInquiry.Sentinels);
        }
        finally
        {
            listener.Dispose();
        }

        // Nothing was persisted (the fault threw before the write): the store is empty,
        // and the switch has returned to inert.
        using var reconciliation = await Client.GetAsync("/api/dev/reconciliation");
        using var reconciliationDocument = await ReadJsonAsync(reconciliation);
        Assert.Equal(0, reconciliationDocument.RootElement.GetProperty("totalCount").GetInt32());

        using var state = await Client.GetAsync("/api/dev/intake-fault");
        using var stateDocument = await ReadJsonAsync(state);
        Assert.Equal(0, stateDocument.RootElement.GetProperty("armed").GetInt32());
        Assert.Equal(1, stateDocument.RootElement.GetProperty("totalInjected").GetInt64());
    }

    [Fact]
    [Trait("CaseId", "IT-FAULT-005")]
    public async Task Dev_surfaces_are_absent_when_not_enabled()
    {
        using var production = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("DevTools:IntakeFault", "false");
            builder.UseSetting("DevTools:Reconciliation", "false");
            builder.UseSetting("DevTools:ScenarioSeeding", "false");
            builder.UseSetting("DevTools:CrmSimulation", "false");
        });
        using var productionClient = production.CreateClient();

        using var fault = await productionClient.GetAsync("/api/dev/intake-fault");
        Assert.Equal(HttpStatusCode.NotFound, fault.StatusCode);

        using var armAttempt = await productionClient.PutAsync(
            "/api/dev/intake-fault", Json("""{ "armCount": 1 }"""));
        Assert.Equal(HttpStatusCode.NotFound, armAttempt.StatusCode);

        using var reconciliation = await productionClient.GetAsync("/api/dev/reconciliation");
        Assert.Equal(HttpStatusCode.NotFound, reconciliation.StatusCode);

        using var byEmail = await productionClient.GetAsync("/api/dev/reconciliation/by-email?email=a@b.cc");
        Assert.Equal(HttpStatusCode.NotFound, byEmail.StatusCode);
    }

    // ── Reconciliation reports ─────────────────────────────────────────────

    [Fact]
    [Trait("CaseId", "IT-RECON-001")]
    public async Task Summary_reports_counts_window_and_the_duplicate_group()
    {
        using var seeded = await Client.PostAsync("/api/dev/scenarios/missing-inquiries", EmptyJson());
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);

        using var response = await Client.GetAsync("/api/dev/reconciliation");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal(26, root.GetProperty("totalCount").GetInt32());

        var counts = root.GetProperty("countByStatus").EnumerateArray()
            .ToDictionary(
                entry => entry.GetProperty("status").GetString()!,
                entry => entry.GetProperty("count").GetInt32());
        Assert.Equal(new[] { "New", "Contacted", "Pending", "Registered", "Closed" }, counts.Keys);
        Assert.Equal(5, counts["New"]);
        Assert.Equal(5, counts["Contacted"]);
        Assert.Equal(5, counts["Pending"]);
        Assert.Equal(5, counts["Registered"]);
        Assert.Equal(6, counts["Closed"]);

        // The recent New rows are all inside the seven-day window.
        Assert.True(root.GetProperty("last7DaysCount").GetInt32() >= 5);

        var duplicates = root.GetProperty("duplicateEmailGroups").EnumerateArray().ToArray();
        var group = Assert.Single(duplicates);
        Assert.Equal("hannah.becker@example.com", group.GetProperty("normalizedEmail").GetString());
        Assert.Equal(2, group.GetProperty("occurrenceCount").GetInt32());
    }

    [Fact]
    [Trait("CaseId", "IT-RECON-002")]
    public async Task Last_seven_days_window_excludes_older_rows()
    {
        // One row at the base instant, then a second ten days later.
        using (var first = await Client.PostAsync("/api/inquiries",
            Json(SyntheticInquiry.With("email", "old@example.com"))))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        factory.Clock.UtcNow = factory.Clock.UtcNow.AddDays(10);

        using (var second = await Client.PostAsync("/api/inquiries",
            Json(SyntheticInquiry.With("email", "recent@example.com"))))
        {
            Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        }

        using var response = await Client.GetAsync("/api/dev/reconciliation");
        using var document = await ReadJsonAsync(response);
        Assert.Equal(2, document.RootElement.GetProperty("totalCount").GetInt32());
        // "now" is base + 10 days, so the window starts at base + 3 days: only the
        // second row falls inside it.
        Assert.Equal(1, document.RootElement.GetProperty("last7DaysCount").GetInt32());
    }

    [Fact]
    [Trait("CaseId", "IT-RECON-003")]
    public async Task By_email_settles_stored_versus_never_stored()
    {
        using (var created = await Client.PostAsync("/api/inquiries",
            Json(SyntheticInquiry.With("email", "findme@example.com"))))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        // Case-insensitive match: a stored row is found (stored-but-hidden).
        using var hit = await Client.GetAsync("/api/dev/reconciliation/by-email?email=FindMe@Example.COM");
        Assert.Equal(HttpStatusCode.OK, hit.StatusCode);
        using var hitDocument = await ReadJsonAsync(hit);
        Assert.Equal("findme@example.com", hitDocument.RootElement.GetProperty("email").GetString());
        Assert.Equal(1, hitDocument.RootElement.GetProperty("matchCount").GetInt32());
        var match = Assert.Single(hitDocument.RootElement.GetProperty("matches").EnumerateArray());
        Assert.Equal("New", match.GetProperty("status").GetString());
        Assert.True(match.GetProperty("id").GetInt32() > 0);

        // No row for the reported email (never-stored).
        using var miss = await Client.GetAsync("/api/dev/reconciliation/by-email?email=nobody@example.com");
        using var missDocument = await ReadJsonAsync(miss);
        Assert.Equal(0, missDocument.RootElement.GetProperty("matchCount").GetInt32());

        // A missing email is a validation problem, never a 500.
        using var invalid = await Client.GetAsync("/api/dev/reconciliation/by-email");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
    }

    // ── Helpers (mirrored from the existing observability/CRM suites) ───────

    private MeterListener StartMetricsListener(InquiryMetrics ownedBy)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == InquiryMetrics.MeterName
                    && ReferenceEquals(instrument.Meter, ownedBy.IntakeOutcomes.Meter))
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

    private static StringContent EmptyJson() => Json("{}");

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
