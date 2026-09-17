using System.Text.Json;

namespace CourseInquiryDashboard.Hosting;

/// <summary>
/// Island asset links resolved from the Vite manifest: the entry script and
/// its stylesheets under the dedicated <c>app/</c> subfolder. Hashed file
/// names are never hard-coded and the Vite dev server is never required at
/// runtime (C7).
/// </summary>
public readonly record struct ViteAssetLinks(string? EntryScript, IReadOnlyList<string> Stylesheets)
{
    public static readonly ViteAssetLinks Empty = new(null, []);
}

/// <summary>
/// Reads <c>wwwroot/app/.vite/manifest.json</c> beneath the content root.
/// A missing or unparseable manifest renders the shell without the island
/// script (guidance only) rather than crashing the page. The manifest is read
/// per request: it is a tiny file and builds can change hashes under a
/// running process.
/// </summary>
public static class ViteManifest
{
    private static readonly string[] ManifestSegments =
        ["wwwroot", "app", ".vite", "manifest.json"];

    public static ViteAssetLinks Resolve(string contentRootPath)
    {
        var manifestPath = System.IO.Path.Combine([contentRootPath, .. ManifestSegments]);
        if (!File.Exists(manifestPath))
        {
            return ViteAssetLinks.Empty;
        }

        // A wrong-shape (but valid-JSON) manifest throws InvalidOperationException
        // from the enumerators, and a rebuild can delete the file between the
        // existence check and the read; both must degrade to the guidance shell,
        // never a 500 from /dashboard.
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (!TryGetEntry(document, out var entry))
            {
                return ViteAssetLinks.Empty;
            }

            var entryScript = entry.GetProperty("file").GetString();
            IReadOnlyList<string> stylesheets = entry.TryGetProperty("css", out var css)
                ? [.. css.EnumerateArray().Select(asset => asset.GetString()).OfType<string>()]
                : [];
            return new ViteAssetLinks(entryScript, stylesheets);
        }
        catch (Exception ex) when (
            ex is JsonException or KeyNotFoundException or InvalidOperationException or IOException)
        {
            return ViteAssetLinks.Empty;
        }
    }

    private static bool TryGetEntry(JsonDocument document, out JsonElement entry)
    {
        if (document.RootElement.TryGetProperty("index.html", out entry))
        {
            return true;
        }

        var mainEntry = document.RootElement.EnumerateObject()
            .FirstOrDefault(property => property.Name.EndsWith("main.tsx", StringComparison.Ordinal));
        entry = mainEntry.Value;
        return entry.ValueKind != JsonValueKind.Undefined;
    }
}
