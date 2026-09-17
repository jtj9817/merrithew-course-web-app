using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CourseInquiryDashboard.Tests.Fixtures;

namespace CourseInquiryDashboard.Tests.Integration;

/// <summary>
/// IT-HOST cases: the Razor shell plus the actual compiled production assets
/// over real HTTP (no Vite dev server, no browser). The lane guarantees a
/// production build exists and reports Blocked when the frontend cannot be
/// built — never a skip.
/// </summary>
[Trait("Category", "Integration")]
public sealed class FrontendHostTests : IAsyncLifetime
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
    [Trait("CaseId", "IT-HOST-001")]
    public async Task Dashboard_serves_shell_with_manifest_entry_and_stylesheets()
    {
        FrontendBuild.AssertNoViteDevServer();
        var entry = FrontendBuild.Manifest.Entry;

        using var response = await Client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(1, CountOccurrences(html, "id=\"dashboard-root\""));
        Assert.Equal(1, CountOccurrences(html, "id=\"inquiry-queue\""));
        Assert.Contains("<a href=\"#inquiry-queue\" class=\"skip-link\">Skip to inquiry queue</a>", html);

        var scriptSources = ExtractAttributes(html, "script", "src")
            .Where(source => IsModuleScript(html, source))
            .ToArray();
        var script = Assert.Single(scriptSources);
        Assert.Equal($"/app/{entry.File}", script);

        var stylesheets = ExtractAttributes(html, "link", "href")
            .Where(href => IsStylesheetLink(html, href))
            .ToArray();
        Assert.Equal(entry.Css.Length, stylesheets.Length);
        Assert.Equal(entry.Css.Select(css => $"/app/{css}").Order(), stylesheets.Order());

        foreach (var forbidden in new[] { ":5173", "@vite/client", "/src/main.tsx", "node_modules" })
        {
            Assert.DoesNotContain(forbidden, html);
        }
    }

    [Fact]
    [Trait("CaseId", "IT-HOST-002")]
    public async Task Every_manifest_referenced_asset_is_served_with_correct_content_type()
    {
        FrontendBuild.AssertNoViteDevServer();
        var closure = FrontendBuild.Manifest.ClosureFromEntry().ToArray();
        Assert.True(closure.Length >= 2,
            $"the manifest closure should contain the entry plus at least one CSS/chunk asset, found {closure.Length}");

        foreach (var asset in closure)
        {
            using var response = await Client.GetAsync($"/app/{asset}");
            Assert.True(response.IsSuccessStatusCode, $"{asset}: expected 200, got {response.StatusCode}");
            Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (asset.EndsWith(".js", StringComparison.Ordinal))
            {
                Assert.Contains(mediaType, new[] { "text/javascript", "application/javascript" });
            }
            else if (asset.EndsWith(".css", StringComparison.Ordinal))
            {
                Assert.Equal("text/css", mediaType);
            }
        }
    }

    [Fact]
    [Trait("CaseId", "IT-HOST-003")]
    public async Task Dashboard_shell_has_guidance_and_no_server_rendered_inquiry_data()
    {
        FrontendBuild.AssertNoViteDevServer();
        var create = await Client.PostAsync("/api/inquiries", Json(
            """
            {
              "firstName": "Avery",
              "lastName": "O'Neill",
              "email": "avery.oneill@example.com",
              "phone": "+1 555 0100",
              "courseName": "Yoga Teacher Training — Fall Cohort",
              "preferredLocation": "Calgary NW",
              "message": "Please send the syllabus & pricing."
            }
            """));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        using var response = await Client.GetAsync("/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        var noscript = Regex.Match(html, "<noscript>(.*?)</noscript>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        Assert.True(noscript.Success, "the shell must render a <noscript> block");
        Assert.Contains("JavaScript", noscript.Value, StringComparison.OrdinalIgnoreCase);

        Assert.Matches(new Regex("loading", RegexOptions.IgnoreCase), html);

        Assert.DoesNotContain("avery.oneill@example.com", html);
    }

    [Fact]
    [Trait("CaseId", "IT-HOST-004")]
    public async Task Rebuild_recreates_app_subfolder_and_preserves_unrelated_static_files()
    {
        FrontendBuild.AssertNoViteDevServer();
        var favicon = Path.Combine(FrontendBuild.WwwRoot, "favicon.ico");
        // A real favicon ships with the app; the case still proves survival by
        // overwriting it with sentinel bytes and restoring the original after.
        var original = File.Exists(favicon) ? File.ReadAllBytes(favicon) : null;
        File.WriteAllBytes(favicon, [0x00, 0x01, 0x02, 0x03]);
        try
        {
            // wwwroot/app is generated output; on a fresh clone it may not exist yet.
            var appDirectory = Path.Combine(FrontendBuild.WwwRoot, "app");
            if (Directory.Exists(appDirectory))
            {
                Directory.Delete(appDirectory, recursive: true);
            }

            var manifest = FrontendBuild.Rebuild();

            Assert.True(File.Exists(Path.Combine(FrontendBuild.WwwRoot, "app", ".vite", "manifest.json")),
                "the rebuild must recreate the Vite manifest");

            using var faviconResponse = await Client.GetAsync("/favicon.ico");
            Assert.Equal(HttpStatusCode.OK, faviconResponse.StatusCode);

            using var entryResponse = await Client.GetAsync($"/app/{manifest.Entry.File}");
            Assert.Equal(HttpStatusCode.OK, entryResponse.StatusCode);
        }
        finally
        {
            if (original is not null)
            {
                File.WriteAllBytes(favicon, original);
            }
            else
            {
                File.Delete(favicon);
            }
        }
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static int CountOccurrences(string haystack, string needle) =>
        Regex.Matches(haystack, Regex.Escape(needle)).Count;

    /// <summary>Extracts attribute values for a tag name across the document.</summary>
    private static IEnumerable<string> ExtractAttributes(string html, string tag, string attribute)
    {
        foreach (var element in Regex.Matches(html, $"<{tag}\\b[^>]*>", RegexOptions.IgnoreCase).Select(m => m.Value))
        {
            var match = Regex.Match(element, $"{attribute}\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                yield return match.Groups[1].Value;
            }
        }
    }

    private static bool IsModuleScript(string html, string source)
    {
        var element = Regex.Matches(html, "<script\\b[^>]*>", RegexOptions.IgnoreCase)
            .Select(m => m.Value)
            .FirstOrDefault(e => e.Contains($"src=\"{source}\"", StringComparison.Ordinal));
        return element is not null && Regex.IsMatch(element, "type\\s*=\\s*\"module\"", RegexOptions.IgnoreCase);
    }

    private static bool IsStylesheetLink(string html, string href)
    {
        var element = Regex.Matches(html, "<link\\b[^>]*>", RegexOptions.IgnoreCase)
            .Select(m => m.Value)
            .FirstOrDefault(e => e.Contains($"href=\"{href}\"", StringComparison.Ordinal));
        return element is not null && Regex.IsMatch(element, "rel\\s*=\\s*\"stylesheet\"", RegexOptions.IgnoreCase);
    }
}
