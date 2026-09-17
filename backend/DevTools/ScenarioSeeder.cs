using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Serialization;
using CourseInquiryDashboard.Services;
using Microsoft.EntityFrameworkCore;

namespace CourseInquiryDashboard.DevTools;

/// <inheritdoc cref="IScenarioSeeder"/>
public sealed class ScenarioSeeder(AppDbContext db, IInquiryService inquiries) : IScenarioSeeder
{
    public IReadOnlyList<ScenarioInfo> Scenarios => ScenarioCatalog.Infos;

    public async Task<int> ClearAsync(CancellationToken cancellationToken = default) =>
        await db.CourseInquiries.ExecuteDeleteAsync(cancellationToken);

    public async Task<SeedResult> SeedAsync(string scenarioName, bool reset, CancellationToken cancellationToken = default)
    {
        if (!ScenarioCatalog.All.TryGetValue(scenarioName, out var scenario))
            throw new KeyNotFoundException($"Unknown scenario '{scenarioName}'.");

        if (reset)
            await ClearAsync(cancellationToken);

        var created = 0;
        var statusUpdates = 0;
        foreach (var seed in scenario.Build())
        {
            // Go through the real create path so seeded rows are validated and run
            // the best-effort CRM sync, exactly like a visitor submission (C5).
            var row = await inquiries.CreateAsync(seed.ToCreateDto(), cancellationToken);
            created++;

            if (seed.Status != Status.New)
            {
                await inquiries.UpdateStatusAsync(row.Id, seed.Status, cancellationToken);
                statusUpdates++;
            }
        }

        var counts = await db.CourseInquiries
            .GroupBy(inquiry => inquiry.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var totalsByStatus = StatusNames.All.ToDictionary(
            name => name,
            name => counts.FirstOrDefault(count => count.Status == Enum.Parse<Status>(name))?.Count ?? 0);

        return new SeedResult(
            scenario.Name,
            reset,
            created,
            statusUpdates,
            totalsByStatus.Values.Sum(),
            totalsByStatus);
    }
}
