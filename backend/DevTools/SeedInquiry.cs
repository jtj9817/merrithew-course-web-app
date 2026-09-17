using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Models.Dtos;

namespace CourseInquiryDashboard.DevTools;

/// <summary>
/// One row in a dev seed scenario: the visitor-facing intake fields plus the
/// <see cref="Models.Status"/> the row should end up in. Seeding creates the row
/// through the normal create path (which always starts <c>New</c>) and then
/// applies <see cref="Status"/> via a status transition, mirroring how a real
/// inquiry moves through the queue.
/// </summary>
public sealed record SeedInquiry(
    string FirstName,
    string LastName,
    string Email,
    string CourseName,
    Status Status = Status.New,
    string? Phone = null,
    string? PreferredLocation = null,
    string? Message = null)
{
    /// <summary>Projects the seed row onto the API intake DTO.</summary>
    public CreateInquiryDto ToCreateDto() => new()
    {
        FirstName = FirstName,
        LastName = LastName,
        Email = Email,
        Phone = Phone,
        CourseName = CourseName,
        PreferredLocation = PreferredLocation,
        Message = Message,
    };
}
