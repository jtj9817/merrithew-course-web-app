using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Serialization;
using CourseInquiryDashboard.Services;
using Microsoft.EntityFrameworkCore;

namespace CourseInquiryDashboard.DevTools;

/// <inheritdoc cref="IScenarioSeeder"/>
public sealed class ScenarioSeeder(AppDbContext db, IInquiryService inquiries, TimeProvider clock) : IScenarioSeeder
{
    // Fixed so a scenario's timestamp spread has the same *shape* every run
    // (the absolute dates still move with the clock); reproducible demos.
    private const int TimelineSeed = 0x5EED;

    public IReadOnlyList<ScenarioInfo> Scenarios => ScenarioCatalog.Infos;

    public async Task<int> ClearAsync(CancellationToken cancellationToken = default) =>
        await db.CourseInquiries.ExecuteDeleteAsync(cancellationToken);

    public async Task<SeedResult> SeedAsync(string scenarioName, bool reset, CancellationToken cancellationToken = default)
    {
        if (!ScenarioCatalog.All.TryGetValue(scenarioName, out var scenario))
            throw new KeyNotFoundException($"Unknown scenario '{scenarioName}'.");

        if (reset)
            await ClearAsync(cancellationToken);

        var now = clock.GetUtcNow().UtcDateTime;
        var timeline = new Random(TimelineSeed);

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

            // The create/transition path stamps every row at "now", which would leave
            // the whole queue sharing one instant and make date sorting look fake. As a
            // dev-only fixture the seeder deliberately backdates each row to a plausible
            // point in the past (C2 permits backward instants) so the dashboard shows a
            // realistic history. The entity is already tracked by this scoped context.
            var entity = await db.CourseInquiries.FindAsync([row.Id], cancellationToken);
            if (entity is not null)
                (entity.CreatedDate, entity.UpdatedDate) = PlanTimestamps(seed.Status, now, timeline);
        }

        await db.SaveChangesAsync(cancellationToken);

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

    /// <summary>
    /// Picks a plausible <c>(CreatedDate, UpdatedDate)</c> for a seeded row. Age is
    /// correlated with lifecycle stage — fresh inquiries are New/Contacted, while
    /// terminal ones (Registered/Closed) have been in the queue longer — with intra-day
    /// jitter so rows don't share a midnight instant. A New row was never worked, so its
    /// <c>UpdatedDate</c> equals its <c>CreatedDate</c>; any other row was last touched
    /// somewhere between creation and now.
    /// </summary>
    private static (DateTime Created, DateTime Updated) PlanTimestamps(Status status, DateTime now, Random rng)
    {
        var (minDays, maxDays) = status switch
        {
            Status.New => (0, 7),
            Status.Contacted => (3, 20),
            Status.Pending => (10, 35),
            Status.Registered => (20, 60),
            Status.Closed => (25, 75),
            _ => (0, 30),
        };

        var created = now
            .AddDays(-rng.Next(minDays, maxDays + 1))
            .AddHours(-rng.Next(0, 24))
            .AddMinutes(-rng.Next(0, 60));

        if (status == Status.New)
            return (created, created);

        // Last touched at 30–90% of the way from creation to now: after the row was
        // created, never in the future.
        var elapsed = (now - created).TotalSeconds;
        var updated = created.AddSeconds(elapsed * (0.3 + rng.NextDouble() * 0.6));
        return (created, updated);
    }
}
