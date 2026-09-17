using System.Diagnostics;
using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Services;
using CourseInquiryDashboard.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Polly.Timeout;

namespace CourseInquiryDashboard.Tests.Unit;

/// <summary>
/// UT-CRM cases: the real production Polly pipeline inside <see cref="SimulatedCrmClient"/>
/// around a scripted in-process attempt (FIX-CRM), observed through a captured log sink
/// (FIX-LOG). Attempt counts, ordering, and outcome categories are exact; backoff delays
/// run for real and are asserted by ordering, never by stopwatch precision (C6).
/// </summary>
[Trait("Category", "Unit")]
public sealed class CrmPipelineTests
{
    private const int InquiryId = 7;

    [Fact]
    [Trait("CaseId", "UT-CRM-001")]
    public async Task Default_success_makes_exactly_one_attempt_and_logs_the_outcome()
    {
        var (attempts, client, logs) = Create(Script.Of(_ => Task.CompletedTask));

        await client.SyncInquiryAsync(Inquiry());

        Assert.Single(attempts.StartedTicks);
        Assert.Contains(logs.Entries, e => IsAttemptStart(e, 1));
        Assert.Contains(logs.Entries, e =>
            HasState(e, "outcome", "success") && HasState(e, "inquiryId", InquiryId));
    }

    [Fact]
    [Trait("CaseId", "UT-CRM-002")]
    public async Task Two_transient_failures_then_success_retry_in_order()
    {
        var (attempts, client, logs) = Create(Script.Of(
            Transient(), Transient(), _ => Task.CompletedTask));

        await client.SyncInquiryAsync(Inquiry());

        Assert.Equal(3, attempts.StartedTicks.Count);
        for (var attempt = 1; attempt <= 3; attempt++)
            Assert.Contains(logs.Entries, e => IsAttemptStart(e, attempt));
        Assert.Contains(logs.Entries, e => HasState(e, "outcome", "success"));
    }

    [Fact]
    [Trait("CaseId", "UT-CRM-003")]
    public async Task Fast_transient_failures_exhaust_after_four_attempts_and_rethrow()
    {
        var (attempts, client, logs) = Create(Script.Of(Transient()));

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => client.SyncInquiryAsync(Inquiry()));

        Assert.Equal(4, attempts.StartedTicks.Count); // original + three retries

        var gaps = attempts.GapsBetweenStarts();
        Assert.InRange(gaps[0].TotalMilliseconds, 90, 200);    // backoff 100 ms
        Assert.InRange(gaps[1].TotalMilliseconds, 190, 400);   // backoff 200 ms
        Assert.InRange(gaps[2].TotalMilliseconds, 385, 1_700); // backoff 400 ms
        Assert.True(gaps[0] < gaps[1] && gaps[1] < gaps[2]);    // exponential ordering

        Assert.Contains(logs.Entries, e => HasState(e, "outcome", "failed"));
    }

    [Fact]
    [Trait("CaseId", "UT-CRM-004")]
    public async Task Attempt_timeout_is_cooperative_and_retried()
    {
        var (attempts, client, logs) = Create(Script.Of(Hang(), _ => Task.CompletedTask));

        await client.SyncInquiryAsync(Inquiry());

        Assert.Equal(2, attempts.StartedTicks.Count); // the timed-out attempt was retried
        Assert.Contains(logs.Entries, e => HasState(e, "outcome", "success"));
    }

    [Fact]
    [Trait("CaseId", "UT-CRM-005")]
    public async Task All_attempts_hang_until_the_outer_budget_ends_the_sync()
    {
        var (attempts, client, logs) = Create(Script.Of(Hang()));

        await Assert.ThrowsAsync<TimeoutRejectedException>(() => client.SyncInquiryAsync(Inquiry()));

        // The outer 2 s total expires during the 400 ms backoff before a fourth attempt.
        Assert.Equal(3, attempts.StartedTicks.Count);
        Assert.Contains(logs.Entries, e => HasState(e, "outcome", "timedOut"));
    }

    [Fact]
    [Trait("CaseId", "UT-CRM-006")]
    public async Task Cancellation_is_never_retried()
    {
        var (attempts, client, logs) = Create(Script.Of(
            _ => Task.FromException(new OperationCanceledException())));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SyncInquiryAsync(Inquiry()));

        Assert.Single(attempts.StartedTicks);
        Assert.Contains(logs.Entries, e => HasState(e, "outcome", "cancelled"));
    }

    [Fact]
    [Trait("CaseId", "UT-CRM-007")]
    public async Task Cancelling_during_backoff_starts_no_further_attempt()
    {
        var attempt1Done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (attempts, client, logs) = Create(Script.Of(async ct =>
        {
            attempt1Done.TrySetResult();
            throw new HttpRequestException("transient");
        }));
        using var cts = new CancellationTokenSource();

        var sync = client.SyncInquiryAsync(Inquiry(), cts.Token);
        await attempt1Done.Task.WaitAsync(TimeSpan.FromSeconds(30));
        cts.Cancel(); // during the first backoff delay

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sync);

        Assert.Single(attempts.StartedTicks);
        Assert.Contains(logs.Entries, e => HasState(e, "outcome", "cancelled"));
    }

    [Fact]
    [Trait("CaseId", "UT-CRM-008")]
    public async Task Permanent_simulated_rejection_is_not_retried()
    {
        var (attempts, client, logs) = Create(Script.Of(
            _ => Task.FromException(new InvalidOperationException("permanent rejection"))));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SyncInquiryAsync(Inquiry()));

        Assert.Single(attempts.StartedTicks);
        Assert.Contains(logs.Entries, e =>
            HasState(e, "outcome", "failed") && HasState(e, "errorType", nameof(InvalidOperationException)));
    }

    [Fact]
    [Trait("CaseId", "UT-CRM-009")]
    public async Task Unknown_exception_types_are_not_retried()
    {
        var (attempts, client, _) = Create(Script.Of(
            _ => Task.FromException(new UnknownCrmException())));

        await Assert.ThrowsAsync<UnknownCrmException>(() => client.SyncInquiryAsync(Inquiry()));

        Assert.Single(attempts.StartedTicks); // the retry allow-list is exact
    }

    [Fact]
    [Trait("CaseId", "UT-CRM-010")]
    public async Task Terminal_scenarios_never_leak_visitor_data_or_raw_exceptions()
    {
        // success
        await RunAndAssertPrivacyAsync(Script.Of(_ => Task.CompletedTask),
            (client, _) => client.SyncInquiryAsync(Inquiry()));

        // retry: transient failures whose messages carry visitor sentinels
        await RunAndAssertPrivacyAsync(Script.Of(Transient(), Transient(), _ => Task.CompletedTask),
            (client, _) => client.SyncInquiryAsync(Inquiry()));

        // exhaustion
        await RunAndAssertPrivacyAsync(Script.Of(Transient()),
            (client, _) => Assert.ThrowsAnyAsync<HttpRequestException>(() => client.SyncInquiryAsync(Inquiry())));

        // timeout
        await RunAndAssertPrivacyAsync(Script.Of(Hang()),
            (client, _) => Assert.ThrowsAsync<TimeoutRejectedException>(() => client.SyncInquiryAsync(Inquiry())));

        // cancellation
        await RunAndAssertPrivacyAsync(Script.Of(CancelWhenEntered()),
            async (client, ct) =>
            {
                var sync = client.SyncInquiryAsync(Inquiry(), ct);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sync);
            });
    }

    private static async Task RunAndAssertPrivacyAsync(
        Script script,
        Func<SimulatedCrmClient, CancellationToken, Task> run)
    {
        var logs = new LogCaptureProvider();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var client = new SimulatedCrmClient(loggerFactory.CreateLogger<SimulatedCrmClient>(), script.NextAsync);

        using var cts = new CancellationTokenSource();
        await run(client, cts.Token);


        logs.AssertPrivacy(SyntheticInquiry.Sentinels);
    }

    private static Func<CancellationToken, Task> Transient() =>
        _ => Task.FromException(new HttpRequestException(
            $"transient failure while syncing {SyntheticInquiry.Email} — {SyntheticInquiry.Message}"));

    private static Func<CancellationToken, Task> Hang() =>
        ct => Task.Delay(Timeout.InfiniteTimeSpan, ct);

    private static Func<CancellationToken, Task> CancelWhenEntered() =>
        ct => Task.FromException(new OperationCanceledException(ct));

    private sealed class UnknownCrmException : Exception;

    /// <summary>Queues scripted attempt outcomes; the final outcome repeats when exhausted.</summary>
    private sealed class Script(params Func<CancellationToken, Task>[] outcomes)
    {
        private readonly Queue<Func<CancellationToken, Task>> outcomes = new(outcomes);
        public static Script Of(params Func<CancellationToken, Task>[] outcomes) => new(outcomes);


        public async Task NextAsync(CancellationToken cancellationToken)
        {
            var outcome = this.outcomes.Count > 1 ? this.outcomes.Dequeue() : this.outcomes.Peek();
            await outcome(cancellationToken);
        }
    }

    private static (AttemptRecorder Attempts, SimulatedCrmClient Client, LogCaptureProvider Logs) Create(
        Script script)
    {
        var attempts = new AttemptRecorder();
        var logs = new LogCaptureProvider();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var client = new SimulatedCrmClient(
            loggerFactory.CreateLogger<SimulatedCrmClient>(),
            attempts.Wrap(script.NextAsync));
        return (attempts, client, logs);
    }

    private sealed class AttemptRecorder
    {
        public List<long> StartedTicks { get; } = [];

        public Func<CancellationToken, Task> Wrap(Func<CancellationToken, Task> next) => async ct =>
        {
            StartedTicks.Add(Stopwatch.GetTimestamp());
            await next(ct);
        };

        public List<TimeSpan> GapsBetweenStarts() => StartedTicks
            .Zip(StartedTicks.Skip(1), (a, b) => Stopwatch.GetElapsedTime(a, b))
            .ToList();
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

    private static bool IsAttemptStart(CapturedLog entry, int attempt) =>
        HasState(entry, "attempt", attempt)
        && entry.Message.Contains("started", StringComparison.OrdinalIgnoreCase);

    private static bool HasState(CapturedLog entry, string key, object value) =>
        entry.State.Any(pair =>
            string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)
            && string.Equals(pair.Value?.ToString(), value.ToString(), StringComparison.Ordinal));
}
