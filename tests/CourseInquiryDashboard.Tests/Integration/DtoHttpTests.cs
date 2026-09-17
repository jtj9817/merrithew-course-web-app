using System.Net;
using System.Text;
using System.Text.Json;
using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Models.Dtos;
using CourseInquiryDashboard.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CourseInquiryDashboard.Tests.Integration;

/// <summary>
/// HTTP contract evidence for the Phase 2 DTOs (C1, C2, C4) through the real
/// MVC pipeline: JSON binding, validation ProblemDetails, unknown-field
/// handling, status names, and response shapes. The probe controller lives in
/// this test assembly and is added to the pipeline only here; it is not a
/// production route and says nothing about the Phase 5 CRUD endpoints, whose
/// cases (IT-API-*) remain open.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DtoHttpTests : IAsyncLifetime
{
    private const string CreateUrl = "/__probe/dto/create";
    private const string StatusUrl = "/__probe/dto/status";
    private const string ResponseUrl = "/__probe/dto/response";
    private const string ResponseNullsUrl = "/__probe/dto/response-nulls";
    private const string PageUrl = "/__probe/dto/page";
    private const string EmptyPageUrl = "/__probe/dto/page-empty";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    private static readonly string[] CreateFieldNames =
        ["courseName", "email", "firstName", "lastName", "message", "phone", "preferredLocation"];
    private static readonly string[] ResponseFieldNames =
        ["courseName", "createdDate", "email", "firstName", "id", "lastName", "message", "phone",
         "preferredLocation", "status", "updatedDate"];

    private readonly InquiryApplicationFactory factory = new();
    private WebApplicationFactory<Program> host = null!;
    private HttpClient client = null!;

    public Task InitializeAsync()
    {
        host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(DtoProbeController).Assembly)));
        client = host.CreateClient();
        DtoProbeController.Reset();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await host.DisposeAsync();
        await factory.DisposeAsync();
    }

    [Fact]
    [Trait("CaseId", "DTO-HTTP-001")]
    public async Task Valid_create_body_binds_and_returns_every_field_verbatim()
    {
        var payload = SyntheticInquiry.Valid();
        payload.Phone = "  +64 21 555 0123  "; // optional whitespace is preserved, never trimmed
        payload.Message = "  Welcome to <b>term 2</b> — kept as data  ";

        var response = await client.PostAsync(CreateUrl, Json(JsonSerializer.Serialize(payload, WebJson)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal(CreateFieldNames, SortedNames(root));
        Assert.Equal(payload.FirstName, root.GetProperty("firstName").GetString());
        Assert.Equal(payload.LastName, root.GetProperty("lastName").GetString());
        Assert.Equal(payload.Email, root.GetProperty("email").GetString());
        Assert.Equal(payload.Phone, root.GetProperty("phone").GetString());
        Assert.Equal(payload.CourseName, root.GetProperty("courseName").GetString());
        Assert.Equal(payload.PreferredLocation, root.GetProperty("preferredLocation").GetString());
        Assert.Equal(payload.Message, root.GetProperty("message").GetString());
        Assert.Equal(1, DtoProbeController.CreateCalls);
    }

    [Fact]
    [Trait("CaseId", "DTO-HTTP-002")]
    public async Task Unknown_and_server_owned_fields_have_no_influence()
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

        var response = await client.PostAsync(CreateUrl, Json(json));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal(CreateFieldNames, SortedNames(root)); // no id/status/createdDate/adminNotes echoed back
        AssertDoesNotEcho(root, "999", "2001-01-01", "admin-sentinel");
        Assert.Equal(1, DtoProbeController.CreateCalls);
    }

    [Fact]
    [Trait("CaseId", "DTO-HTTP-003")]
    public async Task Missing_required_field_returns_problem_json_without_echoing_values()
    {
        const string json = """
        {
          "lastName": "O'Brien",
          "email": "echo-probe@example.com",
          "phone": "+64 21 555 0123",
          "courseName": "Patisserie 301 — evening",
          "preferredLocation": "Auckland CBD",
          "message": "echo-probe-message"
        }
        """;

        var response = await client.PostAsync(CreateUrl, Json(json));

        using var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
        AssertFieldFlagged(problem, "firstName");
        AssertDoesNotEcho(problem.RootElement, "O'Brien", "echo-probe@example.com", "Patisserie 301",
            "+64 21 555 0123", "Auckland CBD", "echo-probe-message");
        Assert.Equal(0, DtoProbeController.CreateCalls); // validation fails before any side effect
    }

    [Fact]
    [Trait("CaseId", "DTO-HTTP-004")]
    public async Task Invalid_values_return_field_scoped_errors_without_echoing_them()
    {
        var tooLong = new string('Z', 101);

        var cases = new (string Field, string Json, string[] Sentinels)[]
        {
            ("firstName", JsonSerializer.Serialize(SyntheticInquiry.With("firstName", tooLong), WebJson), [tooLong]),
            ("email", JsonSerializer.Serialize(SyntheticInquiry.With("email", "no-at-symbol"), WebJson), ["no-at-symbol"]),
            ("lastName", JsonSerializer.Serialize(SyntheticInquiry.With("lastName", "   "), WebJson), []),
        };

        foreach (var (field, json, sentinels) in cases)
        {
            var response = await client.PostAsync(CreateUrl, Json(json));

            using var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
            AssertFieldFlagged(problem, field);
            AssertDoesNotEcho(problem.RootElement, sentinels);
        }

        Assert.Equal(0, DtoProbeController.CreateCalls);
    }

    [Fact]
    [Trait("CaseId", "DTO-HTTP-005")]
    public async Task Malformed_or_missing_bodies_return_problem_json()
    {
        var bodies = new (string Json, string? Field)[]
        {
            ("""{ "firstName": """, null),                        // truncated JSON
            ("", null),                                           // absent body
            ("null", null),                                       // JSON null
            ("""{ "firstName": 42, "lastName": "O'Brien", "email": "a@b.cc", "courseName": "x" }""",
                "firstName"),                                     // wrong field type
        };

        foreach (var (json, field) in bodies)
        {
            var response = await client.PostAsync(CreateUrl, Json(json));

            using var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
            if (field is not null)
                AssertFieldFlagged(problem, field);
        }

        Assert.Equal(0, DtoProbeController.CreateCalls);
    }

    [Fact]
    [Trait("CaseId", "DTO-HTTP-006")]
    public async Task Unsupported_media_type_returns_415()
    {
        var content = new StringContent("""{"firstName":"Aiko"}""", Encoding.UTF8, "text/plain");

        var response = await client.PostAsync(CreateUrl, content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, DtoProbeController.CreateCalls);
    }

    [Fact]
    [Trait("CaseId", "DTO-HTTP-007")]
    public async Task Status_body_accepts_case_insensitive_names_and_writes_canonical_casing()
    {
        var variants = new[]
        {
            ("New", "New"),
            ("contacted", "Contacted"),
            ("  pEnDiNg  ", "Pending"),
            ("REGISTERED", "Registered"),
            ("Closed", "Closed"),
        };

        foreach (var (input, canonical) in variants)
        {
            var response = await client.PutAsync(StatusUrl, Json($"{{\"status\":\"{input}\"}}"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal($"{{\"status\":\"{canonical}\"}}", await response.Content.ReadAsStringAsync());
        }

        Assert.Equal(variants.Length, DtoProbeController.StatusCalls);
    }

    [Fact]
    [Trait("CaseId", "DTO-HTTP-008")]
    public async Task Invalid_status_bodies_return_400_without_echoing_values()
    {
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
            var response = await client.PutAsync(StatusUrl, Json(body));

            using var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
            AssertFieldFlagged(problem, "status");
            AssertDoesNotEcho(problem.RootElement, "Draft", "New,Closed");
        }

        Assert.Equal(0, DtoProbeController.StatusCalls); // never silently defaulted to New
    }

    [Fact]
    [Trait("CaseId", "DTO-HTTP-009")]
    public async Task Inquiry_response_serializes_all_eleven_fields_with_canonical_status_and_Z_timestamps()
    {
        var response = await client.GetAsync(ResponseUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal(ResponseFieldNames, SortedNames(root));
        Assert.Equal(7, root.GetProperty("id").GetInt32());
        Assert.Equal("Aiko", root.GetProperty("firstName").GetString());
        Assert.Equal("Contacted", root.GetProperty("status").GetString()); // canonical name, not a number
        Assert.Equal("2026-03-01T10:00:00Z", root.GetProperty("createdDate").GetString());
        Assert.Equal("2026-03-01T10:00:00Z", root.GetProperty("updatedDate").GetString());

        var nulls = await client.GetAsync(ResponseNullsUrl);
        using var nullDocument = await ReadJsonAsync(nulls);
        var nullRoot = nullDocument.RootElement;

        Assert.Equal(ResponseFieldNames, SortedNames(nullRoot));
        foreach (var field in new[] { "phone", "preferredLocation", "message" })
            Assert.Equal(JsonValueKind.Null, nullRoot.GetProperty(field).ValueKind);
    }

    [Fact]
    [Trait("CaseId", "DTO-HTTP-010")]
    public async Task Page_envelope_serializes_items_and_totals()
    {
        var response = await client.GetAsync(PageUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal(new[] { "items", "page", "pageSize", "totalCount" }, SortedNames(root));
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
        var item = Assert.Single(root.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal(ResponseFieldNames, SortedNames(item));

        var empty = await client.GetAsync(EmptyPageUrl);
        using var emptyDocument = await ReadJsonAsync(empty);
        var emptyRoot = emptyDocument.RootElement;

        Assert.Empty(emptyRoot.GetProperty("items").EnumerateArray());
        Assert.Equal(0, emptyRoot.GetProperty("totalCount").GetInt32());
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<JsonDocument> ReadProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.TryGetProperty("errors", out var errors), "problem details has no errors dictionary");
        Assert.Equal(JsonValueKind.Object, errors.ValueKind);
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

/// <summary>
/// Test-only probe controller. It is added to the MVC pipeline exclusively by
/// <see cref="DtoHttpTests"/> through <c>AddApplicationPart</c>, so no runtime
/// route ever exposes it. Its actions only echo bound DTOs or hand-built
/// response shapes and hold invocation counters proving that invalid requests
/// never reach an action.
/// </summary>
[ApiController]
[Route("__probe/dto")]
public sealed class DtoProbeController : ControllerBase
{
    public static int CreateCalls;
    public static int StatusCalls;

    public static void Reset()
    {
        CreateCalls = 0;
        StatusCalls = 0;
    }

    [HttpPost("create")]
    public ActionResult<CreateInquiryDto> Create([FromBody] CreateInquiryDto dto)
    {
        CreateCalls++;
        return dto;
    }

    [HttpPut("status")]
    public ActionResult<UpdateStatusDto> Status([FromBody] UpdateStatusDto dto)
    {
        StatusCalls++;
        return dto;
    }

    [HttpGet("response")]
    public ActionResult<InquiryResponse> One() => Sample(includeOptionalFields: true);

    [HttpGet("response-nulls")]
    public ActionResult<InquiryResponse> OneWithNulls() => Sample(includeOptionalFields: false);

    [HttpGet("page")]
    public ActionResult<InquiryPageResponse> Page() => new InquiryPageResponse
    {
        Items = [Sample(includeOptionalFields: true)],
        Page = 1,
        PageSize = 20,
        TotalCount = 1,
    };

    [HttpGet("page-empty")]
    public ActionResult<InquiryPageResponse> EmptyPage() => new InquiryPageResponse
    {
        Items = [],
        Page = 1,
        PageSize = 20,
        TotalCount = 0,
    };

    private static InquiryResponse Sample(bool includeOptionalFields) => new()
    {
        Id = 7,
        FirstName = "Aiko",
        LastName = "O'Brien",
        Email = "aiko.obrien+test@example.com",
        Phone = includeOptionalFields ? "+64 21 555 0123" : null,
        CourseName = "Patisserie 301 — evening",
        PreferredLocation = includeOptionalFields ? "Auckland CBD" : null,
        Message = includeOptionalFields ? "Welcome to <b>term 2</b>" : null,
        Status = CourseInquiryDashboard.Models.Status.Contacted,
        CreatedDate = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
        UpdatedDate = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
    };
}
