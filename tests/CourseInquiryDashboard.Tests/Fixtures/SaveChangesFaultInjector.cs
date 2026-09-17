using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CourseInquiryDashboard.Tests.Fixtures;

/// <summary>
/// FIX-INTERCEPT: a real EF Core <see cref="SaveChangesInterceptor"/> whose
/// test-controlled hooks fire inside SavingChanges — deterministic mid-save fault
/// injection (drop the table or delete the target row through an independent
/// connection, cancel the token) without mocking a DbSet.
/// </summary>
public sealed class SaveChangesFaultInjector : SaveChangesInterceptor
{
    public Action<DbContextEventData>? DuringSavingChanges { get; set; }

    public Func<DbContextEventData, CancellationToken, Task>? DuringSavingChangesAsync { get; set; }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        DuringSavingChanges?.Invoke(eventData);
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (DuringSavingChangesAsync is { } hook)
            await hook(eventData, cancellationToken);
        return result;
    }
}
