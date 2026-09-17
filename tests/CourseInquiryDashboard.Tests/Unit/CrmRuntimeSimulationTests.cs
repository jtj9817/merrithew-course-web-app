using System.Diagnostics;
using System.Diagnostics.Metrics;
using CourseInquiryDashboard.Hosting;
using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Services;
using CourseInquiryDashboard.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.Timeout;

namespace CourseInquiryDashboard.Tests.Unit;

/// <summary>
/// UT-CRM-011..017: the production runtime path of <see cref="SimulatedCrmClient"/> —
/// the <see cref="CrmSimulationRuntime"/>-backed constructor executing each configured
/// mode through the real Polly pipeline, observed through a captured log sink. Attempt
/// counts, outcome categories, and recorded results are exact; backoff delays run for
/// real and are asserted by ordering only, never by stopwatch precision (C6).
/// </summary>
[Trait("Category", "Unit")]
public sealed class CrmRuntimeSimulationTests
{
    private const int InquiryId = 7;

    [Fact]
    [Trait("CaseId", "UT-CRM-011")]
    public async Task Runtime_success_makes_one_attempt_and_logs_the_outcome()
    {
        var (runtime, attempts, client, logs) = Create(CrmSimulationMode.Success);

        await client.SyncInquiryAsync(Inquiry());

        Assert.Single(attempts);
        Assert.Contains(logs.Entries, e => HasState(e, "outcome", "success"));
        Assert.True(runtime.TryGetResult(InquiryId, out var result));
        Assert.Equal((CrmSimulationMode.Success, CrmSyncOutcome.Success, 1),
            (result!.Mode, result.Outcome, result.Attempts));
    }

    [Fact]
    [Trait("CaseId", "UT-CRM-012")]
    public async Task Runtime_transient_then_success_retries_in_exponential_order()
    {
        var (runtime, attempts, client, logs) = Create(CrmSimulationMode.TransientThenSuccess,
            transientFailuresBeforeSuccess: 2, latencyMilliseconds: 0);

        await client.SyncInquiryAsync(Inquiry());

        Assert.Equal(3, attempts.Count);
        var gaps = StopwatchGaps(attempts);
        Assert.True(gaps[0] < gaps[1]); // 100 ms then 200 ms backoff — ordering only
        Assert.True(runtime.TryGetResult(InquiryId, out var result));
        Assert.Equal((CrmSyncOutcome.Success, 3), (result!.Outcome, result.Attempts));
        Assert.Equal(2, logs.Entries.Count(e => HasState(e, "outcome", "failed"))); // attempts 1–2
    }

    [Fact]
    [Trait("CaseId", "UT-CRM-013")]
    public async Task Runtime_all_transient_exhausts_after_four_attempts_and_records_failure()
    {
        var (runtime, attempts, client, logs) = Create(CrmSimulationMode.AlwaysTransientFailure,
            latencyMilliseconds: 0);

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => client.SyncInquiryAsync(Inquiry()));

        Assert.Equal(4, attempts.Count);
        Assert.True(runtime.TryGetResult(InquiryId, out var result));
        Assert.Equal((CrmSyncOutcome.Failed, 4), (result!.Outcome, result.Attempts));
        Assert.Contains(logs.Entries, e => HasState(e, "errorType", nameof(HttpRequestException)));
    }

    [Fact]
    [Trait("CaseId", "UT-CRM-014")]
    public async Task Runtime_permanent_failure_is_not_retried()
    {
        var (runtime, attempts, client, _) = Create(CrmSimulationMode.PermanentFailure);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SyncInquiryAsync(Inquiry()));

        Assert.Single(attempts);
        Assert.True(runtime.TryGetResult(InquiryId, out var result));
        Assert.Equal((CrmSyncOutcome.Failed, 1), (result!.Outcome, result.Attempts));
    }

    [Fact]
    [Trait("CaseId", "UT-CRM-015")]
    public async Task Runtime_timeout_ends_at_the_total_budget()
    {
        var (runtime, attempts, client, logs) = Create(CrmSimulationMode.Timeout,
            latencyMilliseconds: 0);

        await Assert.ThrowsAsync<TimeoutRejectedException>(() => client.SyncInquiryAsync(Inquiry()));

        // 500 ms attempt + 100 ms + 200 ms backoff: the 2 s total budget stops the
        // fourth attempt; the cooperative per-attempt timeout is the retryable cause.
        Assert.Equal(3, attempts.Count);
        Assert.True(runtime.TryGetResult(InquiryId, out var result));
        Assert.Equal((CrmSyncOutcome.TimedOut, 3), (result!.Outcome, result.Attempts));
        Assert.Contains(logs.Entries, e => HasState(e, "outcome", "timedOut"));
    }

    [Fact]
    [Trait("CaseId", "UT-CRM-016")]
    public async Task Runtime_internal_cancellation_is_not_retried_and_is_recorded()
    {
        var (runtime, attempts, client, logs) = Create(CrmSimulationMode.InternalCancellation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SyncInquiryAsync(Inquiry()));

        Assert.Single(attempts);
        Assert.True(runtime.TryGetResult(InquiryId, out var result));
        Assert.Equal((CrmSyncOutcome.Cancelled, 1), (result!.Outcome, result.Attempts));
        Assert.Contains(logs.Entries, e => HasState(e, "outcome", "cancelled"));
    }

    [Fact]
    [Trait("CaseId", "UT-CRM-017")]
    public async Task Runtime_modes_never_leak_visitor_data_or_raw_exceptions()
    {
        foreach (var mode in Enum.GetValues<CrmSimulationMode>())
        {
            var (runtime, attempts, client, logs) = Create(mode, latencyMilliseconds: 0);

            if (mode is CrmSimulationMode.Success or CrmSimulationMode.TransientThenSuccess)
            {
                await client.SyncInquiryAsync(Inquiry());
            }
            else
            {
                // Every failing mode rethrows; the runtime records the terminal result.
                await Assert.ThrowsAnyAsync<Exception>(() => client.SyncInquiryAsync(Inquiry()));
            }

            Assert.NotEmpty(attempts);
            logs.AssertPrivacy(SyntheticInquiry.Sentinels);
        }
    }

    [Fact]
    public async Task Runtime_settings_snapshot_survives_a_mid_flight_update()
    {
        var (runtime, attempts, client, _) = Create(CrmSimulationMode.TransientThenSuccess,
            transientFailuresBeforeSuccess: 2, latencyMilliseconds: 0);

        var sync = client.SyncInquiryAsync(Inquiry());
        runtime.Update(new CrmSimulationSettings { Mode = CrmSimulationMode.Success });
        await sync;

        // The in-flight sync kept its original snapshot: 3 attempts, not 1.
        Assert.Equal(3, attempts.Count);
        Assert.True(runtime.TryGetResult(InquiryId, out var result));
        Assert.Equal((CrmSimulationMode.TransientThenSuccess, 3), (result!.Mode, result.Attempts));
    }

    /// <summary>
    /// Attempt recorder: the runtime constructor has no script seam, so attempt order is
    /// observed by wrapping the configured mode with a counting mode of the same shape.
    /// </summary>
    private sealed class AttemptRecorder
    {
        public List<long> StartedTicks { get; } = [];

        public List<long> Attempts => StartedTicks;

        public List<TimeSpan> PairWiseGaps() => StartedTicks
            .Zip(StartedTicks.Skip(1), (a, b) => Stopwatch.GetElapsedTime(a, b))
            .ToList();
    }

    private sealed class CountingRuntime : CrmSimulationRuntime
    {
        public AttemptRecorder Recorder { get; } = new();

        public CountingRuntime(IOptions<CrmSimulationOptions> options) : base(options)
        {
        }

        public override async Task ExecuteAttemptAsync(
            CrmInquiryPayload payload,
            int attempt,
            CrmSimulationSettings snapshot,
            CancellationToken cancellationToken)
        {
            Recorder.StartedTicks.Add(Stopwatch.GetTimestamp());
            await base.ExecuteAttemptAsync(payload, attempt, snapshot, cancellationToken);
        }
    }

    private static (CountingRuntime Runtime, List<long> Attempts, SimulatedCrmClient Client,
        LogCaptureProvider Logs) Create(
        CrmSimulationMode mode, int transientFailuresBeforeSuccess = 2, int latencyMilliseconds = 0)
    {
        var options = Options.Create(new CrmSimulationOptions
        {
            Mode = mode,
            TransientFailuresBeforeSuccess = transientFailuresBeforeSuccess,
            LatencyMilliseconds = latencyMilliseconds,
        });
        var runtime = new CountingRuntime(options);
        var logs = new LogCaptureProvider();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var metrics = new InquiryMetrics(new Meter(InquiryMetrics.MeterName));
        var client = new SimulatedCrmClient(loggerFactory.CreateLogger<SimulatedCrmClient>(), runtime, metrics);

        return (runtime, runtime.Recorder.Attempts, client, logs);
    }

    private static CourseInquiry Inquiry() => new()
    {
        Id = InquiryId,
        FirstName = SyntheticInquiry.FirstName,
        LastName = SyntheticInquiry.LastName,
        Email = SyntheticInquiry.Email,
        Phone = SyntheticInquiry.Phone,
        CourseName = SyntheticInquiry.CourseName,
        PreferredLocation = SyntheticInquiry.PreferredLocation,
        Message = SyntheticInquiry.Message,
        Status = Status.New,
        CreatedDate = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
        UpdatedDate = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
    };

    private static bool HasState(CapturedLog entry, string key, object value) =>
        entry.State.Any(pair =>
            string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)
            && string.Equals(pair.Value?.ToString(), value.ToString(), StringComparison.Ordinal));

    private static List<TimeSpan> StopwatchGaps(List<long> startedTicks) => startedTicks
        .Zip(startedTicks.Skip(1), (a, b) => Stopwatch.GetElapsedTime(a, b))
        .ToList();
}
