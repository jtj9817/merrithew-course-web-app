using CourseInquiryDashboard.Models;

namespace CourseInquiryDashboard.Services;

public interface ICrmClient
{
    Task SyncInquiryAsync(CourseInquiry inquiry, CancellationToken cancellationToken = default);
}
