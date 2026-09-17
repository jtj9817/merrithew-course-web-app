using CourseInquiryDashboard.DevTools;
using CourseInquiryDashboard.Hosting;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CourseInquiryDashboard.Pages;

/// <summary>
/// Staff triage shell (ADR-0004): server-rendered layout plus the compiled
/// React island mount point. The shell carries no inquiry data — the queue is
/// functional only with JavaScript (C7).
/// </summary>
public sealed class DashboardModel : PageModel
{
    private readonly IWebHostEnvironment environment;
    private readonly IConfiguration configuration;

    public DashboardModel(IWebHostEnvironment environment, IConfiguration configuration)
    {
        this.environment = environment;
        this.configuration = configuration;
    }

    public string? EntryScript { get; private set; }
    public IReadOnlyList<string> Stylesheets { get; private set; } = [];

    /// <summary>
    /// True when the dev scenario-seeding tools are enabled; the shell then
    /// exposes a flag the React island reads to render its dev-only switcher.
    /// </summary>
    public bool ScenarioToolsEnabled { get; private set; }

    public void OnGet()
    {
        var links = ViteManifest.Resolve(environment.ContentRootPath);
        EntryScript = links.EntryScript;
        Stylesheets = links.Stylesheets;
        ScenarioToolsEnabled = DevToolsOptions.ScenarioSeedingEnabled(environment, configuration);
    }
}
