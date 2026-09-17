using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Models.Dtos;
using CourseInquiryDashboard.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly.Timeout;

namespace CourseInquiryDashboard.Services;

/// <summary>
/// ADR-0005 orchestration layer: owns the business rules (forced status/timestamps,
/// no-op updates, deterministic filtering/paging, hard delete) and the
/// persist-first/sync-second CRM boundary (C5). Talks to <see cref="AppDbContext"/>
/// directly — EF Core is the unit of work; no repository layer.
/// </summary>
/// <remarks>
/// Timestamps are wall-clock observations from the injected <see cref="TimeProvider"/>,
/// not conflict-detection versions (C2): equal or backward-moving instants are legal.
/// </remarks>
public sealed partial class InquiryService(
    AppDbContext db,
    ICrmClient crmClient,
    TimeProvider clock,
    ILogger<InquiryService> logger) : IInquiryService
{
    public async Task<InquiryResponse> CreateAsync(CreateInquiryDto request, CancellationToken cancellationToken = default)
    {
        // C5: observe cancellation before the write starts — never fabricate success.
        cancellationToken.ThrowIfCancellationRequested();

        var now = clock.GetUtcNow().UtcDateTime; // sampled once for both timestamps (C2)
        var inquiry = new CourseInquiry
        {
            FirstName = request.FirstName!,
            LastName = request.LastName!,
            Email = request.Email!,
            Phone = request.Phone,
            CourseName = request.CourseName!,
            PreferredLocation = request.PreferredLocation,
            Message = request.Message,
            Status = Status.New, // REQ-APP-001: forced, never client-supplied
            CreatedDate = now,
            UpdatedDate = now,
        };

        db.CourseInquiries.Add(inquiry);
        await db.SaveChangesAsync(cancellationToken); // definite failures propagate; no CRM yet

        await SyncCrmAfterCommitAsync(inquiry, cancellationToken);
        return ToResponse(inquiry);
    }

    public async Task<InquiryPageResponse> ListAsync(InquiryListQuery query, CancellationToken cancellationToken = default)
    {
        IQueryable<CourseInquiry> inquiries = db.CourseInquiries.AsNoTracking();
        if (query.Status is { } status)
            inquiries = inquiries.Where(inquiry => inquiry.Status == status); // filter before count/page (C4)

        var totalCount = await inquiries.CountAsync(cancellationToken);

        inquiries = query.Sort == InquirySort.CreatedDateAsc
            ? inquiries.OrderBy(inquiry => inquiry.CreatedDate).ThenBy(inquiry => inquiry.Id)
            : inquiries.OrderByDescending(inquiry => inquiry.CreatedDate).ThenByDescending(inquiry => inquiry.Id);

        var items = await inquiries
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new InquiryPageResponse
        {
            Items = items.Select(ToResponse).ToArray(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<InquiryResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var inquiry = await db.CourseInquiries.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        return inquiry is null ? null : ToResponse(inquiry);
    }

    public async Task<InquiryResponse?> UpdateStatusAsync(int id, Status status, CancellationToken cancellationToken = default)
    {
        if (!StatusNames.IsDefined(status))
            throw new ArgumentException($"The value {(int)status} is not a defined {nameof(Status)} member.", nameof(status));

        var inquiry = await db.CourseInquiries.FirstOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (inquiry is null)
            return null;

        if (inquiry.Status == status)
            return ToResponse(inquiry); // same-status update is a no-op: updatedDate unchanged (C2)

        inquiry.Status = status;
        inquiry.UpdatedDate = clock.GetUtcNow().UtcDateTime;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // C4: the row vanished between lookup and write — map to not-found.
            return null;
        }

        return ToResponse(inquiry);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        => await db.CourseInquiries.Where(row => row.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    /// <summary>
    /// C5 isolation boundary: CRM sync is awaited only after the commit succeeded, and
    /// every CRM outcome — including cancellation and timeout — is contained here. The
    /// stored inquiry and the created response are never rolled back or replayed.
    /// </summary>
    private async Task SyncCrmAfterCommitAsync(CourseInquiry inquiry, CancellationToken cancellationToken)
    {
        try
        {
            await crmClient.SyncInquiryAsync(inquiry, cancellationToken);
        }
        catch (TimeoutRejectedException ex)
        {
            LogCrmIsolatedOutcome(inquiry.Id, "timedOut", ex.GetType().Name);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogCrmIsolatedOutcome(inquiry.Id, "cancelled");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCrmIsolatedOutcome(inquiry.Id, "failed", ex.GetType().Name);
        }
    }

    private static InquiryResponse ToResponse(CourseInquiry inquiry) => new()
    {
        Id = inquiry.Id,
        FirstName = inquiry.FirstName,
        LastName = inquiry.LastName,
        Email = inquiry.Email,
        Phone = inquiry.Phone,
        CourseName = inquiry.CourseName,
        PreferredLocation = inquiry.PreferredLocation,
        Message = inquiry.Message,
        Status = inquiry.Status,
        CreatedDate = inquiry.CreatedDate,
        UpdatedDate = inquiry.UpdatedDate,
    };

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "CRM sync for inquiry {InquiryId} ended with outcome {Outcome}; the stored inquiry is unaffected")]
    private partial void LogCrmIsolatedOutcome(int inquiryId, string outcome);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "CRM sync for inquiry {InquiryId} ended with outcome {Outcome} ({ErrorType}); the stored inquiry is unaffected")]
    private partial void LogCrmIsolatedOutcome(int inquiryId, string outcome, string errorType);
}
