namespace CourseInquiryDashboard.DevTools;

/// <summary>
/// Single source of truth for whether the dev scenario-seeding surface is on:
/// automatically in Development, or in any environment when
/// <c>DevTools:ScenarioSeeding</c> is set true. Used both to map the endpoints
/// (Program.cs) and to reveal the front-end switcher (the dashboard shell), so
/// the two never drift apart.
/// </summary>
public static class DevToolsOptions
{
    public const string ScenarioSeedingKey = "DevTools:ScenarioSeeding";

    public static bool ScenarioSeedingEnabled(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() || configuration.GetValue<bool>(ScenarioSeedingKey);
}
