using System.ComponentModel.DataAnnotations;
using CourseInquiryDashboard.Services;

namespace CourseInquiryDashboard.DevTools;

/// <summary>
/// Dev-only control surface for selecting a deterministic CRM outcome and reading
/// the safe result of a completed inquiry sync. Program.cs maps this group only
/// when the CRM simulation tools are explicitly enabled.
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

        group.MapPut("/", (CrmSimulationSettings settings, CrmSimulationRuntime runtime) =>
        {
            var validationResults = new List<ValidationResult>();
            var valid = Validator.TryValidateObject(
                settings,
                new ValidationContext(settings),
                validationResults,
                validateAllProperties: true)
                && Enum.IsDefined(settings.Mode);

            if (!valid)
            {
                var errors = validationResults
                    .SelectMany(result => result.MemberNames.DefaultIfEmpty("settings")
                        .Select(member => new { member, result.ErrorMessage }))
                    .GroupBy(item => item.member, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(item => item.ErrorMessage ?? "The value is invalid.").ToArray(),
                        StringComparer.OrdinalIgnoreCase);

                if (!Enum.IsDefined(settings.Mode))
                    errors[nameof(settings.Mode)] = ["The CRM simulation mode is not supported."];

                return Results.ValidationProblem(errors);
            }

            runtime.Update(settings);
            return Results.Ok(runtime.Current);
        });

        group.MapGet("/results/{inquiryId:int}", (int inquiryId, CrmSimulationRuntime runtime) =>
            inquiryId > 0 && runtime.TryGetResult(inquiryId, out var result)
                ? Results.Ok(result)
                : Results.NotFound());

        return endpoints;
    }

    private sealed record CrmSimulationModeInfo(CrmSimulationMode Name, string Description);
}
