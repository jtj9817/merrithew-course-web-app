namespace CourseInquiryDashboard.Models.Dtos;

/// <summary>
/// Page envelope for <c>GET /api/inquiries</c> (contract C4). <c>items</c> is
/// the current page slice, while <c>totalCount</c> is the filtered count
/// before pagination.
/// </summary>
public sealed class InquiryPageResponse
{
    public IReadOnlyList<InquiryResponse> Items { get; set; } = [];

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }
}
