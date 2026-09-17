using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Models.Dtos;

namespace CourseInquiryDashboard.Services;

/// <summary>
/// Application-layer boundary over the inquiry store (ADR-0005). Controllers bind and
/// validate DTOs; implementations own the business rules: forced status/timestamps
/// (C2), filter/paging semantics (C4), the persist-first/sync-second create flow (C5),
/// and hard delete (ADR-0006).
/// </summary>
public interface IInquiryService
{
    /// <summary>
    /// Persists a new inquiry with forced defaults, then awaits best-effort CRM sync
    /// (C5). Database failures propagate; CRM failures never do.
    /// </summary>
    /// <exception cref="OperationCanceledException">
    /// The token was cancelled before the write started.
    /// </exception>
    Task<InquiryResponse> CreateAsync(CreateInquiryDto request, CancellationToken cancellationToken = default);

    /// <summary>Returns the filtered, deterministically ordered page envelope (C4).</summary>
    Task<InquiryPageResponse> ListAsync(InquiryListQuery query, CancellationToken cancellationToken = default);

    /// <summary>Returns the inquiry, or <see langword="null"/> when no row has that id.</summary>
    Task<InquiryResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a free-form status transition (C2); same-status updates are no-ops that
    /// leave <c>updatedDate</c> unchanged. Returns <see langword="null"/> when the row is
    /// missing or vanished between lookup and write (C4) — never recreates it.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="status"/> is not a defined enum member (defense in depth for
    /// callers that bypass HTTP, C2).
    /// </exception>
    Task<InquiryResponse?> UpdateStatusAsync(int id, Status status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently removes the inquiry (ADR-0006). Returns <see langword="false"/> when
    /// no row was deleted.
    /// </summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
