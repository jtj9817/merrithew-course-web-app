using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Serialization;
using Microsoft.EntityFrameworkCore;

namespace CourseInquiryDashboard.DevTools;

/// <summary>
/// Dev-only, read-only reconciliation surface (OBS-101 GAP-6): the same
/// count-by-status / last-7-days / duplicate-email / by-email reports the
/// <c>missing-inquiry</c> runbook describes, run as parameterized EF Core LINQ over
/// the live SQLite runtime store — no raw SQL, consistent with the Security answer.
/// Program.cs maps this group only when the reconciliation tools are enabled. The
/// store is synthetic, local-only data (per the Security answer), so the by-email
/// lookup returning visitor rows is acceptable for this opt-in dev surface; nothing
/// here is logged.
/// </summary>
public static class ReconciliationEndpoints
{
    public static IEndpointRouteBuilder MapReconciliationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/dev/reconciliation").WithTags("Dev Reconciliation");

        // Summary: totals staff can reconcile against what the dashboard shows.
        group.MapGet("/", async (AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var since = clock.GetUtcNow().UtcDateTime.AddDays(-7);

            var grouped = await db.CourseInquiries.AsNoTracking()
                .GroupBy(inquiry => inquiry.Status)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);

            // All five statuses, including any with zero rows, in contract order.
            var countByStatus = StatusNames.All
                .Select(name => new StatusCount(
                    name,
                    grouped.FirstOrDefault(entry => entry.Status == Enum.Parse<Status>(name))?.Count ?? 0))
                .ToArray();

            var last7DaysCount = await db.CourseInquiries.AsNoTracking()
                .CountAsync(inquiry => inquiry.CreatedDate >= since, cancellationToken);

            var duplicateEmailGroups = await db.CourseInquiries.AsNoTracking()
                .GroupBy(inquiry => inquiry.Email.Trim().ToLower())
                .Where(group => group.Count() > 1)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Select(group => new DuplicateEmailGroup(group.Key, group.Count()))
                .ToListAsync(cancellationToken);

            return Results.Ok(new
            {
                countByStatus,
                totalCount = countByStatus.Sum(status => status.Count),
                last7DaysCount,
                duplicateEmailGroups,
            });
        });

        // The stored-but-hidden vs never-stored tiebreaker: does a row exist for the
        // reported email, and (if so) in what status / at what time?
        group.MapGet("/by-email", async (string? email, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["email"] = ["An email value is required."],
                });
            }

            var normalized = email.Trim().ToLowerInvariant();
            var rows = await db.CourseInquiries.AsNoTracking()
                .Where(inquiry => inquiry.Email.Trim().ToLower() == normalized)
                .OrderByDescending(inquiry => inquiry.CreatedDate)
                .ThenByDescending(inquiry => inquiry.Id)
                .Select(inquiry => new { inquiry.Id, inquiry.Status, inquiry.CreatedDate, inquiry.UpdatedDate })
                .ToListAsync(cancellationToken);

            var matches = rows
                .Select(row => new EmailMatch(
                    row.Id, StatusNames.ToContractName(row.Status), row.CreatedDate, row.UpdatedDate))
                .ToArray();

            return Results.Ok(new { email = normalized, matchCount = matches.Length, matches });
        });

        return endpoints;
    }

    private sealed record StatusCount(string Status, int Count);

    private sealed record DuplicateEmailGroup(string NormalizedEmail, int OccurrenceCount);

    private sealed record EmailMatch(int Id, string Status, DateTime CreatedDate, DateTime UpdatedDate);
}
