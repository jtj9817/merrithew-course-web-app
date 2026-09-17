using System.ComponentModel.DataAnnotations;

namespace CourseInquiryDashboard.Models.Dtos;

/// <summary>
/// Status update body for <c>PUT /api/inquiries/{id}/status</c> (contract C2).
/// The property is a required nullable enum so an omitted or null JSON
/// <c>status</c> fails validation instead of silently binding to the
/// <see cref="Status.New"/> zero default.
/// </summary>
public sealed class UpdateStatusDto
{
    [Required]
    [EnumDataType(typeof(Status))]
    public Status? Status { get; set; }
}
