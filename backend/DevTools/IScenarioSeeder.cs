namespace CourseInquiryDashboard.DevTools;

/// <summary>Name, human description, and row count of a seed scenario.</summary>
public sealed record ScenarioInfo(string Name, string Description, int Size);

/// <summary>Outcome of a seed run: what was written and the resulting queue shape.</summary>
public sealed record SeedResult(
    string Scenario,
    bool Reset,
    int Created,
    int StatusUpdates,
    int TotalCount,
    IReadOnlyDictionary<string, int> TotalsByStatus);

/// <summary>
/// Dev-only utility that puts the inquiry store into a known state so the
/// dashboard can be exercised against realistic data and specific UI conditions
/// (empty state, a single page, multi-page pagination, an all-New triage inbox).
/// Rows are created through <see cref="Services.IInquiryService"/>, so they pass
/// the same validation and best-effort CRM flow as production traffic.
/// </summary>
public interface IScenarioSeeder
{
    /// <summary>The catalog of scenarios that <see cref="SeedAsync"/> accepts.</summary>
    IReadOnlyList<ScenarioInfo> Scenarios { get; }

    /// <summary>
    /// Seeds <paramref name="scenarioName"/>. When <paramref name="reset"/> is
    /// true (the default for simulation) every existing row is removed first, so
    /// the resulting state is exactly the scenario.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No scenario has that name.</exception>
    Task<SeedResult> SeedAsync(string scenarioName, bool reset, CancellationToken cancellationToken = default);

    /// <summary>Removes every inquiry; returns how many rows were deleted.</summary>
    Task<int> ClearAsync(CancellationToken cancellationToken = default);
}
