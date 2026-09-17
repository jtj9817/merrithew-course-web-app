using System.ComponentModel.DataAnnotations;

namespace CourseInquiryDashboard.Models.Dtos;

/// <summary>
/// Visitor intake payload for <c>POST /api/inquiries</c> (contract C1).
/// Required values default to <see langword="null"/> so an absent field is
/// invalid instead of silently binding to an empty string. Unknown JSON
/// properties (including server-owned id/status/timestamps) are ignored by the
/// web JSON defaults and deliberately have no members here. Values are not
/// trimmed, lowered, truncated, or encoded by validation; optional values may
/// be empty or whitespace and are preserved as submitted.
/// </summary>
public sealed class CreateInquiryDto
{
    [Required, StringLength(100)]
    public string? FirstName { get; set; }

    [Required, StringLength(100)]
    public string? LastName { get; set; }

    [Required, EmailAddress, StringLength(254)]
    public string? Email { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }

    [Required, StringLength(200)]
    public string? CourseName { get; set; }

    [StringLength(200)]
    public string? PreferredLocation { get; set; }

    [StringLength(4000)]
    public string? Message { get; set; }
}
