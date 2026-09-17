namespace CourseInquiryDashboard.Models;

public sealed class CourseInquiry
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
