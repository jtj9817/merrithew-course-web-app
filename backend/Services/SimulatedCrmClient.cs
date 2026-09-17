using CourseInquiryDashboard.Hosting;
using CourseInquiryDashboard.Models;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace CourseInquiryDashboard.Services;

/// <summary>
/// Deterministic in-process CRM simulation (ADR-0007, contract C6). There is no endpoint
/// or credential: completion means success and failure is an exception. The real Polly
/// pipeline wraps either the configured runtime simulation or a test-supplied operation —
/// total 2 s budget → retry (3 retries after the original attempt, exponential
/// 100/200/400 ms, no jitter, transient-only) → cooperative 500 ms per-attempt timeout.
/// </summary>
/// <remarks>
/// Every attempt and terminal outcome are logged with only allow-listed state
/// (inquiry id, attempt, outcome, error type) — never the CRM payload, visitor fields,
/// or raw exceptions. Runtime settings are captured once per sync so a dev-tool change
/// cannot alter an in-flight retry sequence.
/// </remarks>
public sealed partial class SimulatedCrmClient : ICrmClient
{
    /// <summary>Outer total budget for one sync, including backoff delays (C6).</summary>
    private static readonly TimeSpan TotalSyncBudget = TimeSpan.FromSeconds(2);

    /// <summary>Cooperative per-attempt timeout (C6).</summary>
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromMilliseconds(500);

    /// <summary>Base exponential backoff delay: 100, 200, 400 ms (C6).</summary>
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromMilliseconds(100);

    private const int MaxRetryAttempts = 3; // original attempt + three retries = four attempts

    private readonly ILogger<SimulatedCrmClient> logger;
    private readonly CrmSimulationRuntime? runtime;
    private readonly Func<CrmInquiryPayload, int, CancellationToken, Task>? simulation;
    private readonly InquiryMetrics? metrics;

    private readonly ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
        .AddTimeout(new TimeoutStrategyOptions { Timeout = TotalSyncBudget }) // outer budget, never retried
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = MaxRetryAttempts,
            BackoffType = DelayBackoffType.Exponential,
            Delay = RetryBaseDelay,
            UseJitter = false,
            ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Exception is HttpRequestException or TimeoutRejectedException),
        })
        .AddTimeout(new TimeoutStrategyOptions { Timeout = AttemptTimeout })
        .Build();

    /// <summary>Production constructor: execute the runtime mode selected by configuration/dev tools.</summary>
    public SimulatedCrmClient(
        ILogger<SimulatedCrmClient> logger,
        CrmSimulationRuntime runtime,
        InquiryMetrics metrics)
    {
        this.logger = logger;
        this.runtime = runtime;
        this.metrics = metrics;
    }

    /// <summary>Test seam retained for concise scripted attempt outcomes.</summary>
    public SimulatedCrmClient(
        ILogger<SimulatedCrmClient> logger,
        Func<CancellationToken, Task> simulation)
        : this(logger, (_, _, cancellationToken) => simulation(cancellationToken))
    {
    }

    /// <summary>Payload-aware test seam for verifying the external-boundary mapping.</summary>
    public SimulatedCrmClient(
        ILogger<SimulatedCrmClient> logger,
        Func<CrmInquiryPayload, int, CancellationToken, Task> simulation)
    {
        this.logger = logger;
        this.simulation = simulation;
    }

    /// <summary>Records retry consumption for the OBS-101 CRM metrics; a no-op in seam-constructed clients.</summary>
    private void RecordRetries(int attempts)
    {
        if (attempts > 1)
            metrics?.CrmSyncRetries.Add(attempts - 1);
    }

    public async Task SyncInquiryAsync(CourseInquiry inquiry, CancellationToken cancellationToken = default)
    {
        var attempts = 0;
        var settings = runtime?.Current;
        var payload = CrmInquiryPayload.From(inquiry);

        try
        {
            await pipeline.ExecuteAsync(
                async attemptToken =>
                {
                    var attempt = ++attempts;
                    LogAttemptStarted(inquiry.Id, attempt);
                    try
                    {
                        if (runtime is not null)
                        {
                            await runtime.ExecuteAttemptAsync(payload, attempt, settings!, attemptToken)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            await simulation!(payload, attempt, attemptToken).ConfigureAwait(false);
                        }

                        LogAttemptOutcome(inquiry.Id, attempt, "success");
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        LogAttemptOutcome(inquiry.Id, attempt, "cancelled");
                        throw;
                    }
                    catch (OperationCanceledException) when (attemptToken.IsCancellationRequested)
                    {
                        // The attempt's cooperative budget (attempt or total timeout) ended it.
                        LogAttemptOutcome(inquiry.Id, attempt, "timedOut");
                        throw;
                    }
                    catch (OperationCanceledException)
                    {
                        // The simulated operation itself reported cancellation.
                        LogAttemptOutcome(inquiry.Id, attempt, "cancelled");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        LogAttemptOutcome(inquiry.Id, attempt, "failed", ex.GetType().Name);
                        throw;
                    }
                },
                cancellationToken).ConfigureAwait(false);

            LogSyncOutcome(inquiry.Id, "success", attempts);
            RecordSimulationResult(inquiry.Id, settings, CrmSyncOutcome.Success, attempts);
            RecordRetries(attempts);
        }
        catch (TimeoutRejectedException)
        {
            LogSyncOutcome(inquiry.Id, "timedOut", attempts);
            RecordSimulationResult(inquiry.Id, settings, CrmSyncOutcome.TimedOut, attempts);
            RecordRetries(attempts);
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogSyncOutcome(inquiry.Id, "cancelled", attempts);
            RecordSimulationResult(inquiry.Id, settings, CrmSyncOutcome.Cancelled, attempts);
            RecordRetries(attempts);
            throw;
        }
        catch (OperationCanceledException)
        {
            LogSyncOutcome(inquiry.Id, "cancelled", attempts);
            RecordSimulationResult(inquiry.Id, settings, CrmSyncOutcome.Cancelled, attempts);
            RecordRetries(attempts);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSyncOutcome(inquiry.Id, "failed", ex.GetType().Name, attempts);
            RecordSimulationResult(inquiry.Id, settings, CrmSyncOutcome.Failed, attempts);
            RecordRetries(attempts);
            throw;
        }
    }

    private void RecordSimulationResult(
        int inquiryId,
        CrmSimulationSettings? settings,
        CrmSyncOutcome outcome,
        int attempts)
    {
        if (runtime is not null && settings is not null)
            runtime.RecordResult(new CrmSyncResult(inquiryId, settings.Mode, outcome, attempts));
    }

    [LoggerMessage(EventId = 10, Level = LogLevel.Information,
        Message = "CRM sync attempt {Attempt} for inquiry {InquiryId} started")]
    private partial void LogAttemptStarted(int inquiryId, int attempt);

    [LoggerMessage(EventId = 11, Level = LogLevel.Information,
        Message = "CRM sync attempt {Attempt} for inquiry {InquiryId} ended with outcome {Outcome}")]
    private partial void LogAttemptOutcome(int inquiryId, int attempt, string outcome);

    [LoggerMessage(EventId = 12, Level = LogLevel.Warning,
        Message = "CRM sync attempt {Attempt} for inquiry {InquiryId} ended with outcome {Outcome} ({ErrorType})")]
    private partial void LogAttemptOutcome(int inquiryId, int attempt, string outcome, string errorType);

    [LoggerMessage(EventId = 13, Level = LogLevel.Information,
        Message = "CRM sync for inquiry {InquiryId} ended with outcome {Outcome} after {Attempt} attempt(s)")]
    private partial void LogSyncOutcome(int inquiryId, string outcome, int attempt);

    [LoggerMessage(EventId = 14, Level = LogLevel.Warning,
        Message = "CRM sync for inquiry {InquiryId} ended with outcome {Outcome} ({ErrorType}) after {Attempt} attempt(s)")]
    private partial void LogSyncOutcome(int inquiryId, string outcome, string errorType, int attempt);
}
