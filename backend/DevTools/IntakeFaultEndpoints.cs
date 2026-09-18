namespace CourseInquiryDashboard.DevTools;

/// <summary>
/// Dev-only control surface for the intake fault switch. Program.cs maps this group
/// only when the intake-fault tools are explicitly enabled. The update body binds as
/// a loose nullable int so any invalid payload is a 400 validation problem — never a
/// JSON-binding 500 — mirroring C3 error semantics for this opt-in surface. The
/// primary demonstration ("arm one failure, submit, and observe a genuine 500 with
/// no row stored") is driven by the front-end against the real
/// <c>POST /api/inquiries</c>; this endpoint only arms the switch and reports state.
/// </summary>
public static class IntakeFaultEndpoints
{
    public static IEndpointRouteBuilder MapIntakeFaultEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/dev/intake-fault").WithTags("Dev Intake Fault");

        group.MapGet("/", (IntakeFaultRuntime runtime) => Results.Ok(State(runtime)));

        group.MapPut("/", (IntakeFaultUpdateRequest? request, IntakeFaultRuntime runtime) =>
        {
            var armCount = request?.ArmCount;
            if (armCount is null || armCount < 0 || armCount > IntakeFaultRuntime.MaxArmCount)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["armCount"] = [$"The value must be an integer between 0 and {IntakeFaultRuntime.MaxArmCount}."],
                });
            }

            runtime.Arm(armCount.Value);
            return Results.Ok(State(runtime));
        });

        return endpoints;
    }

    private static IntakeFaultState State(IntakeFaultRuntime runtime) =>
        new(runtime.Armed, runtime.TotalInjected);

    /// <summary>Loose update shape: the field is validated above, not by the binder.</summary>
    public sealed record IntakeFaultUpdateRequest(int? ArmCount);

    /// <summary>Safe, non-PII view of the switch: how many creates are armed and how many have failed.</summary>
    public sealed record IntakeFaultState(int Armed, long TotalInjected);
}
