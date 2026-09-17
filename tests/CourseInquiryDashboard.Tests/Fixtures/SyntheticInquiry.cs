using System.Reflection;
using CourseInquiryDashboard.Models.Dtos;

namespace CourseInquiryDashboard.Tests.Fixtures;

/// <summary>
/// FIX-DTO: builder for a synthetic, valid create payload with distinctive
/// sentinel values. Every value is fabricated; catalog cases mutate the
/// returned instance (null, empty, whitespace, boundary lengths) to produce
/// their invalid variants. Sentinels double as privacy-scan tokens, so tests
/// can assert that no submitted value is echoed by an error response or log.
/// </summary>
public static class SyntheticInquiry
{
    public const string FirstName = "Aiko";
    public const string LastName = "O'Brien";
    public const string Email = "aiko.obrien+test@example.com";
    public const string Phone = "+64 21 555 0123";
    public const string CourseName = "Patisserie 301 — evening";
    public const string PreferredLocation = "Auckland CBD";
    public const string Message = "Welcome to <b>term 2</b> — please send the syllabus & pricing.";

    /// <summary>Every visitor value, for whole-payload echo/privacy assertions.</summary>
    public static string[] Sentinels =>
        [FirstName, LastName, Email, Phone, CourseName, PreferredLocation, Message];

    public static CreateInquiryDto Valid() => new()
    {
        FirstName = FirstName,
        LastName = LastName,
        Email = Email,
        Phone = Phone,
        CourseName = CourseName,
        PreferredLocation = PreferredLocation,
        Message = Message,
    };

    /// <summary>Valid payload with one field replaced, for invalid/boundary variants.</summary>
    public static CreateInquiryDto With(string field, string? value)
    {
        var dto = Valid();
        Field(field).SetValue(dto, value);
        return dto;
    }

    /// <summary>Current value of one field, for preservation/no-truncation assertions.</summary>
    public static string? Value(CreateInquiryDto dto, string field) => (string?)Field(field).GetValue(dto);

    private static PropertyInfo Field(string field) =>
        typeof(CreateInquiryDto).GetProperty(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
        ?? throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown create-inquiry field.");
}
