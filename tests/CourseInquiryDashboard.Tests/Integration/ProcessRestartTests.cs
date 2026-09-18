using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Services;
using CourseInquiryDashboard.Tests.Fixtures;
using Microsoft.Data.Sqlite;

namespace CourseInquiryDashboard.Tests.Integration;

/// <summary>
/// IT-APP-020: an OS-level process kill while a CRM sync is pending must not lose the
/// committed inquiry, and the restarted production host performs no CRM attempt for the
/// old row (no durable outbox exists — only future creates sync). The first host is a
/// generated harness process that parks the CRM attempt on file gates; the restarted
/// host is the real backend serving HTTP with the production SimulatedCrmClient, whose
/// console output is scanned for CRM attempt logs.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ProcessRestartTests
{
    private const string CrashEmail = "crash.test@example.com";

    [Fact]
    [Trait("CaseId", "IT-APP-020")]
    public async Task Kill_during_pending_crm_loses_the_sync_but_keeps_the_row()
    {
        var repoRoot = FindRepoRoot();
        var workDir = Path.Combine(Path.GetTempPath(), $"CourseInquiryCrash_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        var dbPath = Path.Combine(workDir, "crash.db");
        var committedMarker = Path.Combine(workDir, "committed.marker");
        var enteredMarker = Path.Combine(workDir, "entered.marker");
        var releaseGate = Path.Combine(workDir, "release.gate");
        var childDir = Path.Combine(workDir, "harness");
        Process? child = null;
        Process? backend = null;
        var backendOutput = new StringBuilder();

        try
        {
            WriteChildProject(childDir, repoRoot);

            child = StartDotNet(
                $"run --project \"{childDir}\" -c Debug --no-launch-profile -- \"{dbPath}\" \"{committedMarker}\" \"{enteredMarker}\" \"{releaseGate}\"",
                repoRoot);
            Assert.True(
                await WaitForAsync(() => File.Exists(committedMarker), TimeSpan.FromMinutes(4)),
                "The harness never reached the parked post-commit CRM sync.");
            var oldInquiryId = int.Parse(await File.ReadAllTextAsync(enteredMarker));

            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync(CancellationToken.None);
            child.Dispose();

            // The committed row survives the abrupt termination, readable independently.
            await using (var probe = new SqliteConnection($"Data Source={dbPath}"))
            {
                await probe.OpenAsync();
                await using var command = probe.CreateCommand();
                command.CommandText = "SELECT COUNT(*) || '|' || MAX(Email) FROM CourseInquiries";
                Assert.Equal($"1|{CrashEmail}", Convert.ToString(await command.ExecuteScalarAsync()));
            }

            var port = GetFreePort();
            backend = StartDotNet(
                $"run --project \"{Path.Combine(repoRoot, "backend")}\" -c Debug --no-launch-profile",
                repoRoot,
                environment: new Dictionary<string, string>
                {
                    ["ConnectionStrings__DefaultConnection"] = $"Data Source={dbPath}",
                    ["ASPNETCORE_ENVIRONMENT"] = "Development",
                    ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}",
                },
                output: backendOutput);

            using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            Assert.True(
                await WaitForAsync(async () => (await http.GetAsync("/api/inquiries")).IsSuccessStatusCode,
                    TimeSpan.FromMinutes(4)),
                "The restarted backend never became ready.");

            using var list = await http.GetAsync("/api/inquiries");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var listJson = await list.Content.ReadAsStringAsync();
            Assert.Contains(CrashEmail, listJson); // row survived restart and is listable
            Assert.DoesNotContain($"inquiry {oldInquiryId} ", backendOutput.ToString(),
                StringComparison.Ordinal); // no CRM replay for the old row

            using var created = await http.PostAsync("/api/inquiries", new StringContent(
                """{"firstName":"After","lastName":"Restart","email":"after.restart@example.com","courseName":"Post-crash 101"}""",
                Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            using var document = System.Text.Json.JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            var newInquiryId = document.RootElement.GetProperty("id").GetInt32();

            Assert.True(
                await WaitForAsync(() => backendOutput.ToString().Contains($"inquiry {newInquiryId} ",
                    StringComparison.Ordinal), TimeSpan.FromSeconds(30)),
                "Future creates still sync — no CRM attempt was logged for the new row.");
        }
        finally
        {
            TryKill(child);
            TryKill(backend);
            TryDeleteDirectory(workDir);
        }
    }

    private static void WriteChildProject(string directory, string repoRoot)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "CrashHarness.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="{Path.Combine(repoRoot, "backend", "CourseInquiryDashboard.csproj")}" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(directory, "Program.cs"), """
            using CourseInquiryDashboard.DevTools;
            using CourseInquiryDashboard.Hosting;
            using CourseInquiryDashboard.Models;
            using CourseInquiryDashboard.Models.Dtos;
            using CourseInquiryDashboard.Services;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.Extensions.Logging.Abstractions;

            var dbPath = args[0];
            var committedMarker = args[1];
            var enteredMarker = args[2];
            var releaseGate = args[3];

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            await using (var setup = new AppDbContext(options))
            {
                await setup.Database.MigrateAsync();
            }

            var crm = new GatedCrmClient(enteredMarker, releaseGate);
            await using var db = new AppDbContext(options);
            var metrics = new InquiryMetrics(new System.Diagnostics.Metrics.Meter(InquiryMetrics.MeterName));
            var service = new InquiryService(db, crm, TimeProvider.System, metrics,
                new IntakeFaultRuntime(), NullLogger<InquiryService>.Instance);
            var create = service.CreateAsync(new CreateInquiryDto
            {
                FirstName = "Crash",
                LastName = "Test",
                Email = "crash.test@example.com",
                CourseName = "Crash Course 101",
            });

            while (!File.Exists(enteredMarker))
                await Task.Delay(20);

            File.WriteAllText(committedMarker, "committed");

            // Parked inside the CRM attempt: the parent kills the process here.
            await create;

            internal sealed class GatedCrmClient(string enteredMarker, string releaseGate) : ICrmClient
            {
                public async Task SyncInquiryAsync(CourseInquiry inquiry, CancellationToken cancellationToken = default)
                {
                    await File.WriteAllTextAsync(enteredMarker, inquiry.Id.ToString());
                    while (!File.Exists(releaseGate))
                        await Task.Delay(50, cancellationToken);
                }
            }
            """);
    }

    private static Process StartDotNet(
        string arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment = null,
        StringBuilder? output = null)
    {
        var startInfo = new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        if (environment is not null)
            foreach (var (key, value) in environment)
                startInfo.Environment[key] = value;

        var process = new Process { StartInfo = startInfo };
        if (output is not null)
        {
            process.OutputDataReceived += (_, eventArgs) => { if (eventArgs.Data is not null) lock (output) output.AppendLine(eventArgs.Data); };
            process.ErrorDataReceived += (_, eventArgs) => { if (eventArgs.Data is not null) lock (output) output.AppendLine(eventArgs.Data); };
        }

        Assert.True(process.Start(), $"Failed to start: dotnet {arguments}");
        if (output is not null)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        return process;
    }

    private static async Task<bool> WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (!cancellation.IsCancellationRequested)
        {
            if (condition())
                return true;
            await Task.Delay(200, CancellationToken.None);
        }

        return false;
    }
    private static async Task<bool> WaitForAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                if (await condition())
                    return true;
            }
            catch (Exception ex) when (ex is HttpRequestException or SocketException)
            {
                // Server still starting up.
            }

            await Task.Delay(200, CancellationToken.None);
        }

        return false;
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CourseInquiryDashboard.slnx")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root from the test location.");
    }

    private static void TryKill(Process? process)
    {
        try
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // A lingering file handle may delay deletion; the temp dir is harmless.
        }
    }
}
