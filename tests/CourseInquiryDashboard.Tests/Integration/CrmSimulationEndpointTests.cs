using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CourseInquiryDashboard.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CourseInquiryDashboard.Tests.Integration;

/// <summary>
/// IT-CRM-SIM cases: the opt-in <c>/api/dev/crm-simulation</c> surface through the full
/// HTTP host — catalog shape, validated updates, safe result lookups, gating outside
/// Development, and the real-client <c>InternalCancellation</c> create (the isolation gap
/// fixed for the runtime simulation). No visitor data is asserted beyond privacy checks.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CrmSimulationEndpointTests : IAsyncLifetime
{
    private readonly InquiryApplicationFactory factory = new();
    private HttpClient? client;

    private HttpClient Client => client ??= factory.CreateClient();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        client?.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    [Trait("CaseId", "IT-CRM-SIM-001")]
    public async Task Catalog_lists_every_mode_with_default_settings()
    {
        using var response = await Client.GetAsync("/api/dev/crm-simulation");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal("Success", root.GetProperty("settings").GetProperty("mode").GetString());
        var names = root.GetProperty("modes").EnumerateArray()
            .Select(mode => mode.GetProperty("name").GetString())
            .ToArray();
        Assert.Equal(
        [
            "Success", "TransientThenSuccess", "AlwaysTransientFailure",
            "PermanentFailure", "Timeout", "InternalCancellation",
        ], names);
        Assert.All(root.GetProperty("modes").EnumerateArray(),
            mode => Assert.False(string.IsNullOrWhiteSpace(mode.GetProperty("description").GetString())));
    }

    [Fact]
    [Trait("CaseId", "IT-CRM-SIM-002")]
    public async Task Update_round_trips_validated_settings()
    {
        using var update = await Client.PutAsync("/api/dev/crm-simulation", Json("""
            { "mode": "TransientThenSuccess", "transientFailuresBeforeSuccess": 3, "latencyMilliseconds": 0 }
            """));

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using var document = await ReadJsonAsync(update);
        Assert.Equal("TransientThenSuccess", document.RootElement.GetProperty("mode").GetString());
        Assert.Equal(3, document.RootElement.GetProperty("transientFailuresBeforeSuccess").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("latencyMilliseconds").GetInt32());

        using var catalog = await Client.GetAsync("/api/dev/crm-simulation");
        using var catalogDocument = await ReadJsonAsync(catalog);
        Assert.Equal("TransientThenSuccess",
            catalogDocument.RootElement.GetProperty("settings").GetProperty("mode").GetString());
    }

    [Fact]
    [Trait("CaseId", "IT-CRM-SIM-003")]
    public async Task Update_rejects_out_of_range_values_with_validation_problems()
    {
        var cases = new[]
        {
            """{ "mode": "TransientThenSuccess", "transientFailuresBeforeSuccess": -1, "latencyMilliseconds": 0 }""",
            """{ "mode": "TransientThenSuccess", "transientFailuresBeforeSuccess": 4, "latencyMilliseconds": 0 }""",
            """{ "mode": "Success", "transientFailuresBeforeSuccess": 0, "latencyMilliseconds": 401 }""",
        };

        foreach (var payload in cases)
        {
            using var response = await Client.PutAsync("/api/dev/crm-simulation", Json(payload));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json",
                response.Content.Headers.ContentType?.MediaType);
        }

        // The last valid runtime state is untouched by rejected updates.
        using var catalog = await Client.GetAsync("/api/dev/crm-simulation");
        using var document = await ReadJsonAsync(catalog);
        Assert.Equal("Success", document.RootElement.GetProperty("settings").GetProperty("mode").GetString());
    }

    [Fact]
    [Trait("CaseId", "IT-CRM-SIM-004")]
    public async Task Update_rejects_unknown_modes()
    {
        using var response = await Client.PutAsync("/api/dev/crm-simulation", Json("""
            { "mode": "NotAMode", "transientFailuresBeforeSuccess": 1, "latencyMilliseconds": 0 }
            """));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "IT-CRM-SIM-005")]
    public async Task Unknown_results_return_404()
    {
        foreach (var id in new[] { 0, 999_999 })
        {
            using var response = await Client.GetAsync($"/api/dev/crm-simulation/results/{id}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    [Trait("CaseId", "IT-CRM-SIM-006")]
    public async Task Endpoints_are_absent_when_the_tool_is_not_enabled()
    {
        using var production = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("DevTools:CrmSimulation", "false");
            builder.UseSetting("DevTools:ScenarioSeeding", "false");
        });
        using var productionClient = production.CreateClient();

        using var catalog = await productionClient.GetAsync("/api/dev/crm-simulation");
        Assert.Equal(HttpStatusCode.NotFound, catalog.StatusCode);

        using var update = await productionClient.PutAsync("/api/dev/crm-simulation", Json("""
            { "mode": "Timeout", "transientFailuresBeforeSuccess": 0, "latencyMilliseconds": 0 }
            """));
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "IT-CRM-SIM-007")]
    public async Task Internal_cancellation_after_commit_returns_201_and_keeps_the_row()
    {
        await using var realClientHost = new RealCrmApplicationFactory(
            mode: "InternalCancellation", latencyMilliseconds: 0);
        using var hostClient = realClientHost.CreateClient();

        using var created = await hostClient.PostAsync("/api/inquiries", Json(
            JsonSerializer.Serialize(SyntheticInquiry.Valid(), WebJson)));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var document = await ReadJsonAsync(created);
        var id = document.RootElement.GetProperty("id").GetInt32();

        using var fetched = await hostClient.GetAsync($"/api/inquiries/{id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        using var fetchedDocument = await ReadJsonAsync(fetched);
        Assert.Equal(SyntheticInquiry.Email, fetchedDocument.RootElement.GetProperty("email").GetString());

        // The isolated outcome is logged (no exception escaped the create) and stays private.
        Assert.Contains(realClientHost.Logs.Entries, entry =>
            entry.State.Any(pair =>
                string.Equals(pair.Key, "outcome", StringComparison.OrdinalIgnoreCase)
                && string.Equals(pair.Value?.ToString(), "cancelled", StringComparison.Ordinal)));
        realClientHost.Logs.AssertPrivacy(SyntheticInquiry.Sentinels);
    }

    [Fact]
    [Trait("CaseId", "IT-CRM-SIM-008")]
    public async Task Runtime_result_is_readable_through_the_endpoint_after_a_real_sync()
    {
        await using var realClientHost = new RealCrmApplicationFactory(
            mode: "TransientThenSuccess", latencyMilliseconds: 0);
        using var hostClient = realClientHost.CreateClient();

        using var created = await hostClient.PostAsync("/api/inquiries", Json(
            JsonSerializer.Serialize(SyntheticInquiry.Valid(), WebJson)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdDocument = await ReadJsonAsync(created);
        var id = createdDocument.RootElement.GetProperty("id").GetInt32();

        using var result = await hostClient.GetAsync($"/api/dev/crm-simulation/results/{id}");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        using var resultDocument = await ReadJsonAsync(result);
        var root = resultDocument.RootElement;
        Assert.Equal(id, root.GetProperty("inquiryId").GetInt32());
        Assert.Equal("TransientThenSuccess", root.GetProperty("mode").GetString());
        Assert.Equal("Success", root.GetProperty("outcome").GetString());
        Assert.Equal(3, root.GetProperty("attempts").GetInt32()); // 2 transient + final success
    }

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static StringContent Json(string payload) =>
        new(payload, Encoding.UTF8, "application/json");

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    /// <summary>
    /// Host that keeps the production <c>SimulatedCrmClient</c> + <c>CrmSimulationRuntime</c>
    /// (unlike <see cref="InquiryApplicationFactory"/>, which swaps in the scripted client),
    /// on its own disposable SQLite file, with a captured log sink.
    /// </summary>
    private sealed class RealCrmApplicationFactory(string mode, int latencyMilliseconds) : WebApplicationFactory<Program>
    {
        private readonly string databasePath =
            Path.Combine(Path.GetTempPath(), $"CourseInquiryRealCrm_{Guid.NewGuid():N}.db");

        public LogCaptureProvider Logs { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                $"Data Source={databasePath}");
            builder.UseSetting("CrmSimulation:Mode", mode);
            builder.UseSetting("CrmSimulation:LatencyMilliseconds",
                latencyMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.ConfigureServices(services =>
                services.AddLogging(logging => logging.AddProvider(Logs)));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                DeleteDatabase();
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            DeleteDatabase();
        }

        private void DeleteDatabase()
        {
            File.Delete(databasePath);
            File.Delete(databasePath + "-wal");
            File.Delete(databasePath + "-shm");
        }
    }
}
