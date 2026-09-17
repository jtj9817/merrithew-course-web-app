namespace CourseInquiryDashboard.DevTools;

/// <summary>
/// Single source of truth for opt-in development surfaces. Each surface is
/// automatically enabled in Development and can be enabled explicitly elsewhere
/// with its own configuration key. Program.cs and the Razor shell use the same
/// checks so mapped endpoints and visible controls cannot drift apart.
/// </summary>
public static class DevToolsOptions
{
    public const string ScenarioSeedingKey = "DevTools:ScenarioSeeding";
    public const string CrmSimulationKey = "DevTools:CrmSimulation";

    public static bool ScenarioSeedingEnabled(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() || configuration.GetValue<bool>(ScenarioSeedingKey);

    public static bool CrmSimulationEnabled(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() || configuration.GetValue<bool>(CrmSimulationKey);
}
