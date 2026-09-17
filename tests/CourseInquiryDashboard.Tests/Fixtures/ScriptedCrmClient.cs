using System.Collections.Concurrent;
using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Services;

namespace CourseInquiryDashboard.Tests.Fixtures;

public sealed record CrmCall(int Id, string FirstName, string LastName, string Email, string? Phone,
    string CourseName, string? PreferredLocation, string? Message, Status Status,
    DateTime CreatedDate, DateTime UpdatedDate);

public sealed class ScriptedCrmClient : ICrmClient
{
    public ConcurrentQueue<Func<CancellationToken, Task>> Outcomes { get; } = new();
    public ConcurrentQueue<CrmCall> Calls { get; } = new();
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource? Gate { get; set; }
    public TimeSpan Delay { get; set; }
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    public async Task SyncInquiryAsync(CourseInquiry inquiry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Enqueue(new(inquiry.Id, inquiry.FirstName, inquiry.LastName, inquiry.Email, inquiry.Phone,
            inquiry.CourseName, inquiry.PreferredLocation, inquiry.Message, inquiry.Status,
            inquiry.CreatedDate, inquiry.UpdatedDate));
        Entered.TrySetResult();
        if (Gate is not null)
            await Gate.Task.WaitAsync(cancellationToken);
        if (Delay > TimeSpan.Zero)
            await Task.Delay(Delay, TimeProvider, cancellationToken);
        if (Outcomes.TryDequeue(out var outcome))
            await outcome(cancellationToken);
    }
}
