namespace CourseInquiryDashboard.Models.Dtos;

/// <summary>
/// Wire representation of a single inquiry (contracts C1 and C4): the entity's
/// eleven documented fields. Status serializes as its canonical name (C2) and
/// timestamps as UTC ISO-8601 ending in <c>Z</c>; nullable visitor fields are
/// present as JSON <c>null</c> rather than omitted. Never use the EF entity
/// itself as a response shape.
/// </summary>
public sealed class InquiryResponse
{
    public int Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public string? PreferredLocation { get; set; }

    public string? Message { get; set; }

    public Status Status { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}
