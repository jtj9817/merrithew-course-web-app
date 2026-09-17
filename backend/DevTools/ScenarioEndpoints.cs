namespace CourseInquiryDashboard.DevTools;

/// <summary>
/// Dev-only HTTP surface the dashboard front-end calls to simulate scenarios:
/// list them, seed one (resetting first by default), or clear the store. These
/// endpoints are mapped only when scenario seeding is enabled (see Program.cs) —
/// never in production unless explicitly opted in — because they mutate and wipe
/// data. Served same-origin as the dashboard, so the React island can call them
/// with a plain <c>fetch</c>.
/// </summary>
public static class ScenarioEndpoints
{
    public static IEndpointRouteBuilder MapScenarioEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/dev/scenarios").WithTags("Dev Scenarios");

        // GET /api/dev/scenarios -> the catalog (name, description, size).
        group.MapGet("/", (IScenarioSeeder seeder) => Results.Ok(seeder.Scenarios));

        // POST /api/dev/scenarios/{name}?reset=true -> put the store into that state.
        group.MapPost("/{name}", async (
            string name, bool? reset, IScenarioSeeder seeder, CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await seeder.SeedAsync(name, reset ?? true, cancellationToken));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new
                {
                    error = $"Unknown scenario '{name}'.",
                    available = seeder.Scenarios.Select(scenario => scenario.Name),
                });
            }
        });

        // DELETE /api/dev/scenarios -> clear every inquiry (the empty state).
        group.MapDelete("/", async (IScenarioSeeder seeder, CancellationToken cancellationToken) =>
            Results.Ok(new { removed = await seeder.ClearAsync(cancellationToken) }));

        return endpoints;
    }
}
