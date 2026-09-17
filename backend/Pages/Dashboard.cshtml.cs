using CourseInquiryDashboard.Hosting;
using Microsoft.AspNetCore.Mvc;
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

    public DashboardModel(IWebHostEnvironment environment)
    {
        this.environment = environment;
    }

    public string? EntryScript { get; private set; }
    public IReadOnlyList<string> Stylesheets { get; private set; } = [];

    public void OnGet()
    {
        var links = ViteManifest.Resolve(environment.ContentRootPath);
        EntryScript = links.EntryScript;
        Stylesheets = links.Stylesheets;
    }
}
