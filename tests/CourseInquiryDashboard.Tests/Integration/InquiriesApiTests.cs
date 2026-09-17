using System.Net;
using System.Text;
using System.Text.Json;
using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CourseInquiryDashboard.Tests.Integration;

/// <summary>
/// IT-API cases: the five production inquiry endpoints through the full HTTP host
/// (FIX-HTTP) over a per-test real SQLite file. Routing, binding, JSON, ProblemDetails,
/// paging envelopes, sanitized failures, and OpenAPI facts are proven only here.
/// </summary>
[Trait("Category", "Integration")]
public sealed class InquiriesApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    private static readonly DateTime T0 = new(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

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
    [Trait("CaseId", "IT-API-001")]
    public async Task Create_returns_201_with_location_resolving_to_the_resource()
    {
        var response = await Client.PostAsync("/api/inquiries", Json(SyntheticInquiry.Valid()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal(new[] { "courseName", "createdDate", "email", "firstName", "id", "lastName", "message",
            "phone", "preferredLocation", "status", "updatedDate" }, SortedNames(root));
        Assert.True(root.GetProperty("id").GetInt32() > 0);
        Assert.Equal("New", root.GetProperty("status").GetString());
        Assert.Equal("2026-03-01T10:00:00Z", root.GetProperty("createdDate").GetString());
        Assert.Equal("2026-03-01T10:00:00Z", root.GetProperty("updatedDate").GetString());
        Assert.Equal(SyntheticInquiry.Email, root.GetProperty("email").GetString());
        Assert.Equal(SyntheticInquiry.Phone, root.GetProperty("phone").GetString());

        Assert.NotNull(response.Headers.Location);
        using var fetched = await Client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        using var fetchedDocument = await ReadJsonAsync(fetched);
        Assert.Equal(root.GetProperty("id").GetInt32(), fetchedDocument.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(root.GetProperty("email").GetString(), fetchedDocument.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    [Trait("CaseId", "IT-API-002")]
    public async Task Invalid_bodies_return_validation_problems_without_echoing_values()
    {
        var tooLong = new string('Z', 101);
        var cases = new (string Json, string[] Sentinels)[]
        {
            ("""{ "lastName": "O'Brien", "email": "a@b.cc", "courseName": "x" }""", []),
            (JsonSerializer.Serialize(SyntheticInquiry.With("firstName", tooLong), WebJson), [tooLong]),
            (JsonSerializer.Serialize(SyntheticInquiry.With("email", "not-an-email"), WebJson), ["not-an-email"]),
            ("{ \"firstName\": ", []),                                   // malformed JSON
            ("", []),                                                     // absent body
            ("null", []),                                                 // JSON null
            ("""{ "firstName": 42, "lastName": "O'Brien", "email": "a@b.cc", "courseName": "x" }""", []), // wrong type
        };

        foreach (var (json, sentinels) in cases)
        {
            var response = await Client.PostAsync("/api/inquiries", Json(json));

            using var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
            AssertDoesNotEcho(problem.RootElement, sentinels);
        }

        Assert.Equal(0, await ListTotalCountAsync()); // no row or CRM side effect
        Assert.Empty(factory.Crm.Calls);
    }

    [Fact]
    [Trait("CaseId", "IT-API-003")]
    public async Task Server_owned_fields_in_the_body_have_no_influence()
    {
        const string json = """
        {
          "firstName": "Aiko",
          "lastName": "O'Brien",
          "email": "aiko.obrien+test@example.com",
          "courseName": "Patisserie 301 — evening",
          "id": 999,
          "status": "Closed",
          "createdDate": "2001-01-01T00:00:00Z",
          "updatedDate": "2001-01-01T00:00:00Z",
          "adminNotes": "admin-sentinel"
        }
        """;

        var response = await Client.PostAsync("/api/inquiries", Json(json));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;
        Assert.NotEqual(999, root.GetProperty("id").GetInt32());
        Assert.Equal("New", root.GetProperty("status").GetString());
        Assert.Equal("2026-03-01T10:00:00Z", root.GetProperty("createdDate").GetString());
        Assert.Equal("2026-03-01T10:00:00Z", root.GetProperty("updatedDate").GetString());
    }

    [Fact]
    [Trait("CaseId", "IT-API-004")]
    public async Task Unsupported_media_type_and_verb_are_rejected_by_the_pipeline()
    {
        var textPlain = await Client.PostAsync("/api/inquiries",
            new StringContent("""{"firstName":"Aiko"}""", Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, textPlain.StatusCode);

        var patch = await Client.PatchAsync("/api/inquiries/1/status",
            Json("""{"status":"Pending"}"""));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, patch.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "IT-API-005")]
    public async Task Unusable_id_paths_fail_routing_with_404()
    {
        foreach (var id in new[] { "0", "-1", "abc", "2147483648" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/inquiries/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await Client.PutAsync($"/api/inquiries/{id}/status", Json("""{"status":"Pending"}"""))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Client.DeleteAsync($"/api/inquiries/{id}")).StatusCode);
        }
    }

    [Fact]
    [Trait("CaseId", "IT-API-006")]
    public async Task Invalid_status_body_is_400_even_for_a_missing_id()
    {
        var response = await Client.PutAsync("/api/inquiries/999999/status", Json("""{"status":"Bogus"}"""));

        using var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
        AssertFieldFlagged(problem, "status"); // validation runs before resource lookup
    }

    [Fact]
    [Trait("CaseId", "IT-API-007")]
    public async Task Get_by_id_returns_200_or_problem_details_404()
    {
        var ids = await SeedAsync((Status.New, T0));

        var found = await Client.GetAsync($"/api/inquiries/{ids[0]}");
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        using var foundDocument = await ReadJsonAsync(found);
        Assert.Equal(SyntheticInquiry.Email, foundDocument.RootElement.GetProperty("email").GetString());

        var missing = await Client.GetAsync("/api/inquiries/999999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await missing.Content.ReadAsStringAsync());
        Assert.Equal(404, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.RootElement.TryGetProperty("title", out var title)
            ? title.GetString() : null));
        Assert.False(string.IsNullOrEmpty(problem.RootElement.TryGetProperty("type", out var type)
            ? type.GetString() : null));
    }

    [Fact]
    [Trait("CaseId", "IT-API-008")]
    public async Task Status_update_accepts_dirty_names_and_stores_canonical_casing()
    {
        var ids = await SeedAsync((Status.New, T0));

        var response = await Client.PutAsync($"/api/inquiries/{ids[0]}/status", Json("""{"status":"  pEnDiNg  "}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        Assert.Equal("Pending", document.RootElement.GetProperty("status").GetString());

        using var fetched = await ReadJsonAsync(await Client.GetAsync($"/api/inquiries/{ids[0]}"));
        Assert.Equal("Pending", fetched.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    [Trait("CaseId", "IT-API-009")]
    public async Task Invalid_status_updates_are_400_and_never_change_the_stored_status()
    {
        var ids = await SeedAsync((Status.New, T0));
        var bodies = new[]
        {
            "{}",
            """{"status":null}""",
            """{"status":0}""",
            """{"status":1}""",
            """{"status":"0"}""",
            """{"status":"1"}""",
            """{"status":true}""",
            """{"status":[]}""",
            """{"status":{}}""",
            """{"status":"New,Closed"}""",
            """{"status":"Draft"}""",
        };

        foreach (var body in bodies)
        {
            var response = await Client.PutAsync($"/api/inquiries/{ids[0]}/status", Json(body));

            using var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
            AssertFieldFlagged(problem, "status");
            AssertDoesNotEcho(problem.RootElement, "Draft", "New,Closed");
        }

        using var fetched = await ReadJsonAsync(await Client.GetAsync($"/api/inquiries/{ids[0]}"));
        Assert.Equal("New", fetched.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    [Trait("CaseId", "IT-API-010")]
    public async Task Same_status_update_over_http_is_a_no_op()
    {
        var created = await CreateValidAsync();
        factory.Clock.UtcNow = factory.Clock.UtcNow.AddMinutes(5);

        var response = await Client.PutAsync($"/api/inquiries/{created}/status", Json("""{"status":"new"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        Assert.Equal("2026-03-01T10:00:00Z", document.RootElement.GetProperty("updatedDate").GetString());
        Assert.Equal(
            document.RootElement.GetProperty("createdDate").GetString(),
            document.RootElement.GetProperty("updatedDate").GetString());
    }

    [Fact]
    [Trait("CaseId", "IT-API-011")]
    public async Task Valid_status_body_for_a_missing_id_is_404()
    {
        await SeedAsync((Status.New, T0));
        var before = await ListTotalCountAsync();

        var response = await Client.PutAsync("/api/inquiries/999999/status", Json("""{"status":"Pending"}"""));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, await ListTotalCountAsync());
    }

    [Fact]
    [Trait("CaseId", "IT-API-012")]
    public async Task Delete_is_204_then_404_forever()
    {
        var ids = await SeedAsync((Status.New, T0));

        var first = await Client.DeleteAsync($"/api/inquiries/{ids[0]}");
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Empty(await first.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/inquiries/{ids[0]}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.DeleteAsync($"/api/inquiries/{ids[0]}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.DeleteAsync("/api/inquiries/999999")).StatusCode);
        Assert.Equal(0, await ListTotalCountAsync());
    }

    [Fact]
    [Trait("CaseId", "IT-API-013")]
    public async Task Empty_store_returns_an_empty_default_envelope()
    {
        var response = await Client.GetAsync("/api/inquiries");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;
        Assert.Equal(new[] { "items", "page", "pageSize", "totalCount" }, SortedNames(root));
        Assert.Empty(root.GetProperty("items").EnumerateArray());
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(0, root.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    [Trait("CaseId", "IT-API-014")]
    public async Task List_paging_and_sorting_are_deterministic()
    {
        // Rows 5, 6, 7 share one createdDate; ids follow insertion order 1..25.
        await SeedAsync(Enumerable.Range(1, 25).Select(i =>
            (Status.New, i is >= 5 and <= 7 ? T0.AddMinutes(5) : T0.AddMinutes(i))).ToArray());

        var defaults = await ReadPageAsync("/api/inquiries");
        Assert.Equal(25, defaults.totalCount);
        Assert.Equal(1, defaults.page);
        Assert.Equal(20, defaults.pageSize);
        Assert.Equal(20, defaults.ids.Count); // default page size 20
        Assert.Equal(Enumerable.Range(6, 20).Reverse(), defaults.ids); // desc: 25..6

        var page2 = await ReadPageAsync("/api/inquiries?page=2&pageSize=5");
        Assert.Equal(2, page2.page);
        Assert.Equal(5, page2.pageSize);
        Assert.Equal(25, page2.totalCount);
        Assert.Equal(new[] { 20, 19, 18, 17, 16 }, page2.ids);

        var asc = await ReadPageAsync("/api/inquiries?sort=createdDateAsc&page=2&pageSize=5");
        Assert.Equal(new[] { 6, 7, 8, 9, 10 }, asc.ids);

        var desc = await ReadPageAsync("/api/inquiries?sort=createdDateDesc&page=1&pageSize=3");
        Assert.Equal(new[] { 25, 24, 23 }, desc.ids);
    }

    [Fact]
    [Trait("CaseId", "IT-API-015")]
    public async Task Status_filter_applies_before_count_and_paging()
    {
        await SeedAsync(
            (Status.New, T0),
            (Status.New, T0.AddMinutes(1)),
            (Status.Closed, T0.AddMinutes(2)),
            (Status.Pending, T0.AddMinutes(3)),
            (Status.Closed, T0.AddMinutes(4)),
            (Status.Registered, T0.AddMinutes(5)),
            (Status.Contacted, T0.AddMinutes(6)));

        var closed = await ReadPageAsync("/api/inquiries?status=Closed");
        Assert.Equal(2, closed.totalCount); // filtered count only
        Assert.Equal(new[] { 5, 3 }, closed.ids);

        var closedFirstPage = await ReadPageAsync("/api/inquiries?status=Closed&page=1&pageSize=1");
        Assert.Equal(new[] { 5 }, closedFirstPage.ids);
        Assert.Equal(2, closedFirstPage.totalCount);

        var all = await ReadPageAsync("/api/inquiries");
        Assert.Equal(7, all.totalCount); // omitting the filter includes Closed
    }

    [Fact]
    [Trait("CaseId", "IT-API-016")]
    public async Task Invalid_query_values_return_400_never_clamped()
    {
        var queries = new[]
        {
            "?page=0",
            "?page=-1",
            "?page=abc",
            "?page=",
            "?pageSize=0",
            "?pageSize=101",
            "?pageSize=abc",
            "?page=2147483647&pageSize=100", // (page - 1) * pageSize overflows Int32
            "?status=",                       // supplied empty is invalid
            "?status=Draft",
            "?sort=newest",
        };

        foreach (var query in queries)
        {
            var response = await Client.GetAsync($"/api/inquiries{query}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(problem.RootElement.TryGetProperty("errors", out var errors)
                && errors.EnumerateObject().Any(), $"query '{query}' produced no field errors");
        }
    }

    [Fact]
    [Trait("CaseId", "IT-API-017")]
    public async Task A_valid_page_beyond_the_end_is_200_with_empty_items()
    {
        await SeedAsync((Status.New, T0), (Status.New, T0.AddMinutes(1)), (Status.New, T0.AddMinutes(2)));

        var response = await Client.GetAsync("/api/inquiries?page=99");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        Assert.Empty(document.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(3, document.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    [Trait("CaseId", "IT-API-018")]
    public async Task Crm_failure_after_commit_is_not_an_http_error()
    {
        factory.Crm.Outcomes.Enqueue(_ => Task.FromException(new InvalidOperationException("permanent CRM failure")));

        var response = await Client.PostAsync("/api/inquiries", Json(SyntheticInquiry.Valid()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        var id = document.RootElement.GetProperty("id").GetInt32();
        using var fetched = await ReadJsonAsync(await Client.GetAsync($"/api/inquiries/{id}"));
        Assert.Equal(SyntheticInquiry.Email, fetched.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    [Trait("CaseId", "IT-API-019")]
    public async Task Database_failure_returns_sanitized_500_in_both_environments()
    {
        await using var probe = new SqliteConnection(factory.ConnectionString);
        await probe.OpenAsync();
        var drop = probe.CreateCommand();
        drop.CommandText = "DROP TABLE IF EXISTS \"CourseInquiries\"";
        var injector = new SaveChangesFaultInjector
        {
            DuringSavingChangesAsync = async (_, _) => await drop.ExecuteNonQueryAsync(),
        };
        factory.SaveChangesInterceptors.Add(injector);

        using var productionHost = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var productionClient = productionHost.CreateClient();

        var forbidden = SyntheticInquiry.Sentinels.Concat(new[]
        {
            "Sqlite", "CourseInquiries", "Data Source", "at CourseInquiryDashboard", "System.", "stack",
        }).ToArray();

        foreach (var (environment, activeClient) in new[]
        {
            ("Development", Client),
            ("Production", productionClient),
        })
        {
            var response = await activeClient.PostAsync("/api/inquiries", Json(SyntheticInquiry.Valid()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(500, problem.RootElement.GetProperty("status").GetInt32());
            Assert.False(string.IsNullOrEmpty(problem.RootElement.TryGetProperty("title", out var title)
                ? title.GetString() : null));
            Assert.False(string.IsNullOrEmpty(problem.RootElement.TryGetProperty("type", out var type)
                ? type.GetString() : null));
            AssertDoesNotEcho(problem.RootElement, forbidden);
        }

        factory.Logs.AssertPrivacy(SyntheticInquiry.Sentinels);
    }

    [Fact]
    [Trait("CaseId", "IT-API-020")]
    public async Task Openapi_documents_all_five_endpoints_and_shapes_are_executable()
    {
        var swagger = await Client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        using var document = JsonDocument.Parse(await swagger.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/inquiries", out var collection));
        Assert.True(collection.TryGetProperty("post", out _));
        Assert.True(collection.TryGetProperty("get", out _));
        Assert.True(paths.TryGetProperty("/api/inquiries/{id}", out var item));
        Assert.True(item.TryGetProperty("get", out _));
        Assert.True(item.TryGetProperty("delete", out _));
        Assert.True(paths.TryGetProperty("/api/inquiries/{id}/status", out var status));
        Assert.True(status.TryGetProperty("put", out _));

        var created = await CreateValidAsync();
        Assert.True(created > 0);

        var updated = await Client.PutAsync($"/api/inquiries/{created}/status", Json("""{"status":"Contacted"}"""));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
    }

    [Fact]
    [Trait("CaseId", "IT-API-021")]
    public async Task Created_rows_are_immediately_readable_by_id_and_in_the_list()
    {
        var created = await CreateValidAsync();

        using var byId = await ReadJsonAsync(await Client.GetAsync($"/api/inquiries/{created}"));
        Assert.Equal(SyntheticInquiry.Email, byId.RootElement.GetProperty("email").GetString());

        var page = await ReadPageAsync("/api/inquiries");
        Assert.Equal(1, page.totalCount);
        Assert.Equal([created], page.ids);
    }

    private async Task<int> CreateValidAsync()
    {
        var response = await Client.PostAsync("/api/inquiries", Json(SyntheticInquiry.Valid()));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        return document.RootElement.GetProperty("id").GetInt32();
    }

    private async Task<List<int>> SeedAsync(params (Status Status, DateTime Created)[] rows)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(factory.ConnectionString)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var ids = new List<int>();
        foreach (var (status, created) in rows)
        {
            var row = new CourseInquiry
            {
                FirstName = SyntheticInquiry.FirstName,
                LastName = SyntheticInquiry.LastName,
                Email = SyntheticInquiry.Email,
                Phone = SyntheticInquiry.Phone,
                CourseName = SyntheticInquiry.CourseName,
                PreferredLocation = SyntheticInquiry.PreferredLocation,
                Message = SyntheticInquiry.Message,
                Status = status,
                CreatedDate = created,
                UpdatedDate = created,
            };
            db.CourseInquiries.Add(row);
            await db.SaveChangesAsync();
            ids.Add(row.Id);
        }

        return ids;
    }

    private async Task<(List<int> ids, int page, int pageSize, int totalCount)> ReadPageAsync(string url)
    {
        var response = await Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;
        return (
            root.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList(),
            root.GetProperty("page").GetInt32(),
            root.GetProperty("pageSize").GetInt32(),
            root.GetProperty("totalCount").GetInt32());
    }

    private async Task<int> ListTotalCountAsync() => (await ReadPageAsync("/api/inquiries")).totalCount;

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static StringContent Json(object payload) => Json(JsonSerializer.Serialize(payload, WebJson));

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<JsonDocument> ReadProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.TryGetProperty("errors", out var errors), "problem details has no errors dictionary");
        Assert.True(errors.EnumerateObject().Any(), "problem details errors dictionary is empty");
        return document;
    }

    private static void AssertFieldFlagged(JsonDocument problem, string field)
        => Assert.Contains(problem.RootElement.GetProperty("errors").EnumerateObject(),
            property => property.Name.Contains(field, StringComparison.OrdinalIgnoreCase));

    private static void AssertDoesNotEcho(JsonElement element, params string[] sentinels)
    {
        var text = string.Join('\n', Strings(element));
        foreach (var sentinel in sentinels)
            Assert.DoesNotContain(sentinel, text);
    }

    private static IEnumerable<string> Strings(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    yield return property.Name;
                    foreach (var value in Strings(property.Value))
                        yield return value;
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    foreach (var value in Strings(item))
                        yield return value;
                break;
            case JsonValueKind.String:
                yield return element.GetString() ?? string.Empty;
                break;
        }
    }

    private static string[] SortedNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal).ToArray();
}
