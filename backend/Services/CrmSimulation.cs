using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CourseInquiryDashboard.Models;
using Microsoft.Extensions.Options;

namespace CourseInquiryDashboard.Services;

[JsonConverter(typeof(JsonStringEnumConverter<CrmSimulationMode>))]
public enum CrmSimulationMode
{
    Success,
    TransientThenSuccess,
    AlwaysTransientFailure,
    PermanentFailure,
    Timeout,
    InternalCancellation,
}

[JsonConverter(typeof(JsonStringEnumConverter<CrmSyncOutcome>))]
public enum CrmSyncOutcome
{
    Success,
    Failed,
    TimedOut,
    Cancelled,
}

/// <summary>Startup defaults for the in-process CRM simulation.</summary>
public sealed class CrmSimulationOptions
{
    public const string SectionName = "CrmSimulation";

    [EnumDataType(typeof(CrmSimulationMode))]
    public CrmSimulationMode Mode { get; set; } = CrmSimulationMode.Success;

    [Range(0, 3)]
    public int TransientFailuresBeforeSuccess { get; set; } = 2;

    [Range(0, 400)]
    public int LatencyMilliseconds { get; set; } = 100;
}

/// <summary>Immutable settings captured once at the start of each CRM sync.</summary>
public sealed record CrmSimulationSettings
{
    [EnumDataType(typeof(CrmSimulationMode))]
    public CrmSimulationMode Mode { get; init; } = CrmSimulationMode.Success;

    [Range(0, 3)]
    public int TransientFailuresBeforeSuccess { get; init; } = 2;

    [Range(0, 400)]
    public int LatencyMilliseconds { get; init; } = 100;
}

/// <summary>
/// Explicit payload handed to the simulated external boundary. It deliberately
/// contains visitor data a real CRM needs, but no logging code accepts this type.
/// </summary>
public sealed record CrmInquiryPayload(
    int InquiryId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string CourseName,
    string? PreferredLocation,
    string? Message,
    Status Status,
    DateTime CreatedDate,
    DateTime UpdatedDate)
{
    public static CrmInquiryPayload From(CourseInquiry inquiry) => new(
        inquiry.Id,
        inquiry.FirstName,
        inquiry.LastName,
        inquiry.Email,
        inquiry.Phone,
        inquiry.CourseName,
        inquiry.PreferredLocation,
        inquiry.Message,
        inquiry.Status,
        inquiry.CreatedDate,
        inquiry.UpdatedDate);
}

/// <summary>Safe, non-PII diagnostic result exposed only by the dev-tool endpoint.</summary>
public sealed record CrmSyncResult(
    int InquiryId,
    CrmSimulationMode Mode,
    CrmSyncOutcome Outcome,
    int Attempts);

/// <summary>
/// Thread-safe runtime for the configured simulation. Settings are snapshotted per
/// sync, so changing the dev control cannot alter an in-flight retry sequence.
/// Results retain only bounded, non-PII metadata for the front-end demonstration.
/// </summary>
public class CrmSimulationRuntime
{
    private const int MaxRetainedResults = 100;
    private readonly ConcurrentDictionary<int, CrmSyncResult> results = new();
    private readonly ConcurrentQueue<int> resultOrder = new();
    private CrmSimulationSettings settings;

    public CrmSimulationRuntime(IOptions<CrmSimulationOptions> options)
    {
        var configured = options.Value;
        settings = new CrmSimulationSettings
        {
            Mode = configured.Mode,
            TransientFailuresBeforeSuccess = configured.TransientFailuresBeforeSuccess,
            LatencyMilliseconds = configured.LatencyMilliseconds,
        };
        Validate(settings);
    }

    public CrmSimulationSettings Current => Volatile.Read(ref settings);

    public void Update(CrmSimulationSettings next)
    {
        Validate(next);
        Volatile.Write(ref settings, next);
    }

    public virtual async Task ExecuteAttemptAsync(
        CrmInquiryPayload payload,
        int attempt,
        CrmSimulationSettings snapshot,
        CancellationToken cancellationToken)
    {
        if (payload.InquiryId <= 0)
            throw new ArgumentException("A persisted inquiry id is required.", nameof(payload));

        if (snapshot.LatencyMilliseconds > 0)
            await Task.Delay(snapshot.LatencyMilliseconds, cancellationToken).ConfigureAwait(false);

        switch (snapshot.Mode)
        {
            case CrmSimulationMode.Success:
                return;
            case CrmSimulationMode.TransientThenSuccess:
                if (attempt <= snapshot.TransientFailuresBeforeSuccess)
                    throw new HttpRequestException("Simulated transient CRM failure.");
                return;
            case CrmSimulationMode.AlwaysTransientFailure:
                throw new HttpRequestException("Simulated transient CRM failure.");
            case CrmSimulationMode.PermanentFailure:
                throw new InvalidOperationException("Simulated permanent CRM rejection.");
            case CrmSimulationMode.Timeout:
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                return;
            case CrmSimulationMode.InternalCancellation:
                throw new OperationCanceledException("Simulated CRM-side cancellation.");
            default:
                throw new InvalidOperationException("Unsupported CRM simulation mode.");
        }
    }

    public void RecordResult(CrmSyncResult result)
    {
        results[result.InquiryId] = result;
        resultOrder.Enqueue(result.InquiryId);

        while (results.Count > MaxRetainedResults && resultOrder.TryDequeue(out var expiredId))
            results.TryRemove(expiredId, out _);
    }

    public bool TryGetResult(int inquiryId, out CrmSyncResult? result) =>
        results.TryGetValue(inquiryId, out result);

    private static void Validate(CrmSimulationSettings candidate)
    {
        Validator.ValidateObject(candidate, new ValidationContext(candidate), validateAllProperties: true);
        if (!Enum.IsDefined(candidate.Mode))
            throw new ValidationException("The CRM simulation mode is not supported.");
    }
}
