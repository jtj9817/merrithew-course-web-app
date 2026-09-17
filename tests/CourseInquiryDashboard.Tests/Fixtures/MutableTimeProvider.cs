namespace CourseInquiryDashboard.Tests.Fixtures;

public sealed class MutableTimeProvider : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
