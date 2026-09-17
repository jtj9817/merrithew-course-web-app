using CourseInquiryDashboard.Models;

namespace CourseInquiryDashboard.Models.Dtos;

/// <summary>
/// Sort directions accepted by the list endpoint (contract C4). The wire spellings are
/// exactly <c>createdDateDesc</c> and <c>createdDateAsc</c>.
/// </summary>
public enum InquirySort
{
    CreatedDateDesc,

    CreatedDateAsc,
}

/// <summary>
/// The validated, service-facing form of the list query (C4): status filter, 1-based
/// page, bounded page size, and the deterministic CreatedDate+Id sort. The API boundary
/// validates raw query strings before instances reach the service.
/// </summary>
public sealed record InquiryListQuery(
    Status? Status = null,
    int Page = 1,
    int PageSize = InquiryListQuery.DefaultPageSize,
    InquirySort Sort = InquirySort.CreatedDateDesc)
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;
}
