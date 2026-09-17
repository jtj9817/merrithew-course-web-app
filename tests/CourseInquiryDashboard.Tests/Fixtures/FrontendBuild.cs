using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace CourseInquiryDashboard.Tests.Fixtures;

internal sealed class FrontendBuildBlockedException(string message) : Exception(message);

/// <summary>
/// Frontend host-lane build fixture (catalog §2.2): guarantees the production
/// Vite build exists, exposes the parsed manifest, rebuilds on demand for
/// IT-HOST-004, and proves the Vite dev server is absent. A missing or failing
/// build reports <see cref="FrontendBuildBlockedException"/> (Blocked) — never
/// a skip and never green, mirroring the SQL Server lane.
/// </summary>
internal static class FrontendBuild
{
    private const int DevServerPort = 5173;
    private static readonly object Gate = new();
    private static ViteManifestDocument? loaded;

    public static string WwwRoot { get; } = Path.Combine(LocateRepositoryRoot(), "backend", "wwwroot");

    public static ViteManifestDocument Manifest
    {
        get
        {
            lock (Gate)
            {
                return loaded ??= LoadOrBuild();
            }
        }
    }

    /// <summary>Forces a fresh production build (IT-HOST-004) and re-parses the manifest.</summary>
    public static ViteManifestDocument Rebuild()
    {
        lock (Gate)
        {
            RunPnpmBuild();
            return loaded = ParseManifest()
                ?? throw new FrontendBuildBlockedException(
                    "Blocked: frontend rebuild did not produce a Vite manifest under backend/wwwroot/app.");
        }
    }

    /// <summary>The host lane runs against built assets only; a listening dev server is a failure.</summary>
    public static void AssertNoViteDevServer()
    {
        using var probe = new TcpClient();
        try
        {
            probe.Connect(IPAddress.Loopback, DevServerPort);
        }
        catch (SocketException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"A Vite dev server is listening on port {DevServerPort}; the IT-HOST lane requires the production build with no dev server.");
    }

    private static ViteManifestDocument LoadOrBuild()
    {
        var manifest = ParseManifest();
        if (manifest is null)
        {
            RunPnpmBuild();
            manifest = ParseManifest();
        }

        return manifest ?? throw new FrontendBuildBlockedException(
            "Blocked: frontend build not produced — no Vite manifest at backend/wwwroot/app/.vite/manifest.json " +
            "and 'pnpm --dir frontend build' did not create one.");
    }

    private static ViteManifestDocument? ParseManifest()
    {
        var path = Path.Combine(WwwRoot, "app", ".vite", "manifest.json");
        if (!File.Exists(path))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var chunks = new Dictionary<string, ViteChunk>();
        ViteChunk? entry = null;
        ViteChunk? mainTsxFallback = null;
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var chunk = ReadChunk(property.Name, property.Value);
            chunks[property.Name] = chunk;
            if (property.Name == "index.html" && chunk.IsEntry)
            {
                entry = chunk;
            }
            else if (property.Name.EndsWith("main.tsx", StringComparison.Ordinal) && chunk.IsEntry)
            {
                mainTsxFallback = chunk;
            }
        }

        entry ??= mainTsxFallback;
        return entry is null ? null : new ViteManifestDocument(entry, chunks);
    }

    private static ViteChunk ReadChunk(string key, JsonElement element)
    {
        string FileOf(JsonElement e) => e.GetProperty("file").GetString()!;
        string[] ArrayOf(string name) => element.TryGetProperty(name, out var values)
            ? values.EnumerateArray().Select(v => v.GetString()!).ToArray()
            : [];
        return new ViteChunk(
            key,
            FileOf(element),
            ArrayOf("css"),
            ArrayOf("imports"),
            ArrayOf("dynamicImports"),
            element.TryGetProperty("isEntry", out var isEntry) && isEntry.GetBoolean());
    }

    private static void RunPnpmBuild()
    {
        var repositoryRoot = LocateRepositoryRoot();
        var info = new ProcessStartInfo("pnpm", "--dir frontend build")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        try
        {
            using var process = Process.Start(info) ?? throw new FrontendBuildBlockedException(
                "Blocked: could not start 'pnpm' for the frontend build.");
            if (!process.WaitForExit(TimeSpan.FromMinutes(5)))
            {
                process.Kill(entireProcessTree: true);
                throw new FrontendBuildBlockedException("Blocked: 'pnpm --dir frontend build' timed out after 5 minutes.");
            }

            if (process.ExitCode != 0)
            {
                var stderr = process.StandardError.ReadToEnd();
                throw new FrontendBuildBlockedException(
                    $"Blocked: 'pnpm --dir frontend build' exited with {process.ExitCode}. {stderr}");
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new FrontendBuildBlockedException(
                $"Blocked: pnpm is not available on PATH for the frontend build ({ex.Message}).");
        }
    }

    private static string LocateRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "frontend"))
                && Directory.Exists(Path.Combine(directory.FullName, "backend"))
                && File.Exists(Path.Combine(directory.FullName, "TODO.md")))
            {
                return directory.FullName;
            }
        }

        throw new FrontendBuildBlockedException(
            "Blocked: could not locate the repository root (frontend/backend/TODO.md) from the test output directory.");
    }
}

internal sealed record ViteChunk(
    string Key,
    string File,
    string[] Css,
    string[] Imports,
    string[] DynamicImports,
    bool IsEntry);

internal sealed record ViteManifestDocument(ViteChunk Entry, IReadOnlyDictionary<string, ViteChunk> Chunks)
{
    /// <summary>
    /// Transitive asset closure from the entry (file, css, imports, dynamicImports),
    /// as reachable paths under the public base — proving real emission, not a stub.
    /// </summary>
    public IEnumerable<string> ClosureFromEntry()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<ViteChunk>();
        queue.Enqueue(Entry);
        while (queue.Count > 0)
        {
            var chunk = queue.Dequeue();
            if (!seen.Add(chunk.File))
            {
                continue;
            }

            foreach (var css in chunk.Css)
            {
                seen.Add(css);
            }

            foreach (var importedKey in chunk.Imports.Concat(chunk.DynamicImports))
            {
                if (Chunks.TryGetValue(importedKey, out var imported))
                {
                    queue.Enqueue(imported);
                }
            }
        }

        return seen;
    }
}
