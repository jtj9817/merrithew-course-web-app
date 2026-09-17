using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Serialization;

namespace CourseInquiryDashboard.Models.Dtos;

/// <summary>
/// Query-string model for <c>GET /api/inquiries</c> (contract C4). String-bound so a
/// supplied-but-blank value (<c>?status=</c>, <c>?page=</c>) is distinguishable from
/// omission and is rejected with 400 rather than silently defaulted.
/// </summary>
public sealed class ListInquiriesQueryDto : IValidatableObject
{
    private const string SortDesc = "createdDateDesc";
    private const string SortAsc = "createdDateAsc";
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Page { get; set; }

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? PageSize { get; set; }

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Status { get; set; }

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Sort { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var pageOk = TryParseInteger(Page, 1, int.MaxValue, out var page);
        if (!pageOk)
            yield return new ValidationResult("page must be an integer of at least 1.", ["page"]);

        var sizeOk = TryParseInteger(PageSize, 1, InquiryListQuery.MaxPageSize, out var pageSize);
        if (!sizeOk)
            yield return new ValidationResult(
                $"pageSize must be an integer between 1 and {InquiryListQuery.MaxPageSize}.", ["pageSize"]);

        // Reject if (page - 1) * pageSize exceeds Int32.MaxValue, computed without overflowing (C4).
        if (pageOk && sizeOk && (long)(page - 1) * pageSize > int.MaxValue)
            yield return new ValidationResult("The requested page start lies outside the addressable range.", ["page"]);

        if (Status is not null && !StatusNames.TryParse(Status, out _))
            yield return new ValidationResult(
                $"status must be one of: {StatusNames.CommaSeparated}.", ["status"]);

        if (Sort is not null && Sort is not (SortDesc or SortAsc))
            yield return new ValidationResult($"sort must be '{SortDesc}' or '{SortAsc}'.", ["sort"]);
    }

    /// <summary>Converts to the validated, service-facing query model.</summary>
    public InquiryListQuery ToQuery() => new(
        Status: StatusNames.TryParse(Status, out var status) ? status : null,
        Page: int.TryParse(Page, NumberStyles.None, CultureInfo.InvariantCulture, out var page) && page >= 1
            ? page : 1,
        PageSize: int.TryParse(PageSize, NumberStyles.None, CultureInfo.InvariantCulture, out var size)
            && size is >= 1 and <= InquiryListQuery.MaxPageSize
            ? size : InquiryListQuery.DefaultPageSize,
        Sort: Sort == SortAsc ? InquirySort.CreatedDateAsc : InquirySort.CreatedDateDesc);

    private static bool TryParseInteger(string? raw, int minimum, int maximum, out int value)
    {
        value = 0;
        if (raw is null)
            return true; // omission is allowed

        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value >= minimum
            && value <= maximum;
    }
}
