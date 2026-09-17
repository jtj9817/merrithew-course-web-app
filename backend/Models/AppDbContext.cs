using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CourseInquiryDashboard.Models;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>Named CHECK constraint keeping stored status names within the five contract values (C2, C8).</summary>
    public const string StatusCheckConstraintName = "CK_CourseInquiries_Status";

    private const string StatusValuesSql = "Status IN ('New', 'Contacted', 'Pending', 'Registered', 'Closed')";

    /// <summary>
    /// UTC <see cref="DateTime"/> semantics (C8): SQLite stores timestamps without a kind, so a
    /// provider value is re-tagged as UTC on read and local values are normalized instant-precisely
    /// on write. Unspecified input is assumed to already be UTC rather than silently shifted.
    /// </summary>
    private static readonly ValueConverter<DateTime, DateTime> UtcDateTimeConverter = new(
        convertToProviderExpression: value => NormalizeUtc(value),
        convertFromProviderExpression: value => NormalizeUtc(value));

    public DbSet<CourseInquiry> CourseInquiries => Set<CourseInquiry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var inquiry = modelBuilder.Entity<CourseInquiry>();

        inquiry.ToTable("CourseInquiries", table => table.HasCheckConstraint(StatusCheckConstraintName, StatusValuesSql));

        inquiry.Property(i => i.FirstName).IsRequired().HasMaxLength(100);
        inquiry.Property(i => i.LastName).IsRequired().HasMaxLength(100);
        inquiry.Property(i => i.Email).IsRequired().HasMaxLength(254);
        inquiry.Property(i => i.Phone).IsRequired(false).HasMaxLength(50);
        inquiry.Property(i => i.CourseName).IsRequired().HasMaxLength(200);
        inquiry.Property(i => i.PreferredLocation).IsRequired(false).HasMaxLength(200);
        inquiry.Property(i => i.Message).IsRequired(false).HasMaxLength(4000);

        inquiry.Property(i => i.Status)
            .IsRequired()
            .HasConversion(new EnumToStringConverter<Status>());

        inquiry.Property(i => i.CreatedDate).IsRequired().HasConversion(UtcDateTimeConverter);
        inquiry.Property(i => i.UpdatedDate).IsRequired().HasConversion(UtcDateTimeConverter);
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
