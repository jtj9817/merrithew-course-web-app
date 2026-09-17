using CourseInquiryDashboard.Services;

namespace CourseInquiryDashboard.DevTools;

/// <summary>
/// Dev-only control surface for selecting a deterministic CRM outcome and reading
/// the safe result of a completed inquiry sync. Program.cs maps this group only
/// when the CRM simulation tools are explicitly enabled. The update body binds as
/// loose strings/ints so any invalid payload is a 400 validation problem — never a
/// JSON-binding 500 — mirroring C3 error semantics for this opt-in surface.
/// </summary>
public static class CrmSimulationEndpoints
{
    private static readonly CrmSimulationModeInfo[] Modes =
    [
        new(CrmSimulationMode.Success, "One successful CRM attempt."),
        new(CrmSimulationMode.TransientThenSuccess, "Transient failures are retried, then the configured attempt succeeds."),
        new(CrmSimulationMode.AlwaysTransientFailure, "All four attempts fail transiently; the inquiry remains stored."),
        new(CrmSimulationMode.PermanentFailure, "One permanent rejection is not retried; the inquiry remains stored."),
        new(CrmSimulationMode.Timeout, "Attempts time out until the two-second CRM budget expires."),
        new(CrmSimulationMode.InternalCancellation, "The simulated CRM cancels independently of the request; the inquiry remains stored."),
    ];

    public static IEndpointRouteBuilder MapCrmSimulationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/dev/crm-simulation").WithTags("Dev CRM Simulation");

        group.MapGet("/", (CrmSimulationRuntime runtime) => Results.Ok(new
        {
            settings = runtime.Current,
            modes = Modes,
        }));

        group.MapPut("/", (CrmSimulationUpdateRequest? request, CrmSimulationRuntime runtime) =>
        {
            var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            var mode = CrmSimulationMode.Success;
            if (string.IsNullOrEmpty(request?.Mode)
                || !Enum.TryParse<CrmSimulationMode>(request.Mode, out mode)
                || !Enum.IsDefined(mode))
            {
                errors["mode"] = [$"The mode must be one of: {string.Join(", ", Enum.GetNames<CrmSimulationMode>())}."];
                mode = default;
            }

            var failures = ValidateRange(
                request?.TransientFailuresBeforeSuccess, min: 0, max: 3, "transientFailuresBeforeSuccess", errors);
            var latency = ValidateRange(
                request?.LatencyMilliseconds, min: 0, max: 400, "latencyMilliseconds", errors);

            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            runtime.Update(new CrmSimulationSettings
            {
                Mode = mode,
                TransientFailuresBeforeSuccess = failures,
                LatencyMilliseconds = latency,
            });
            return Results.Ok(runtime.Current);
        });

        group.MapGet("/results/{inquiryId:int}", (int inquiryId, CrmSimulationRuntime runtime) =>
            inquiryId > 0 && runtime.TryGetResult(inquiryId, out var result)
                ? Results.Ok(result)
                : Results.NotFound());

        return endpoints;
    }

    /// <summary>Loose update shape: every field is validated below, not by the binder.</summary>
    public sealed record CrmSimulationUpdateRequest(
        string? Mode,
        int? TransientFailuresBeforeSuccess,
        int? LatencyMilliseconds);

    private static int ValidateRange(int? value, int min, int max, string field,
        Dictionary<string, string[]> errors)
    {
        if (value is null || value < min || value > max)
        {
            errors[field] = [$"The value must be an integer between {min} and {max}."];
            return min;
        }
        return value.Value;
    }

    private sealed record CrmSimulationModeInfo(CrmSimulationMode Name, string Description);
}
