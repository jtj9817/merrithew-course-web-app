using System.ComponentModel.DataAnnotations;
using CourseInquiryDashboard.Models.Dtos;
using CourseInquiryDashboard.Tests.Fixtures;

namespace CourseInquiryDashboard.Tests.Unit;

/// <summary>
/// UT-VAL-001…008: contract C1 intake validation on <see cref="CreateInquiryDto"/>.
/// Pure DataAnnotations evidence — no database, no HTTP. Length limits count
/// UTF-16 code units, so surrogate-pair strings are exercised at the boundary.
/// </summary>
[Trait("Category", "Unit")]
public class CreateInquiryValidationTests
{
    [Fact]
    [Trait("CaseId", "UT-VAL-001")]
    public void Complete_valid_payload_passes_and_keeps_every_value()
    {
        var dto = SyntheticInquiry.Valid();

        Assert.Empty(Validate(dto));
        Assert.Equal(SyntheticInquiry.FirstName, dto.FirstName);
        Assert.Equal(SyntheticInquiry.LastName, dto.LastName);
        Assert.Equal(SyntheticInquiry.Email, dto.Email);
        Assert.Equal(SyntheticInquiry.Phone, dto.Phone);
        Assert.Equal(SyntheticInquiry.CourseName, dto.CourseName);
        Assert.Equal(SyntheticInquiry.PreferredLocation, dto.PreferredLocation);
        Assert.Equal(SyntheticInquiry.Message, dto.Message);
    }

    [Fact]
    [Trait("CaseId", "UT-VAL-002")]
    public void Every_property_defaults_to_null_not_an_empty_string()
    {
        var dto = new CreateInquiryDto();

        Assert.Null(dto.FirstName);
        Assert.Null(dto.LastName);
        Assert.Null(dto.Email);
        Assert.Null(dto.Phone);
        Assert.Null(dto.CourseName);
        Assert.Null(dto.PreferredLocation);
        Assert.Null(dto.Message);
    }

    [Theory]
    [InlineData(nameof(CreateInquiryDto.FirstName))]
    [InlineData(nameof(CreateInquiryDto.LastName))]
    [InlineData(nameof(CreateInquiryDto.Email))]
    [InlineData(nameof(CreateInquiryDto.CourseName))]
    [Trait("CaseId", "UT-VAL-002")]
    public void Missing_required_field_flags_only_that_field(string field)
        => AssertOnlyFieldFlagged(Validate(SyntheticInquiry.With(field, null)), field);

    [Theory]
    [InlineData(nameof(CreateInquiryDto.FirstName), "")]
    [InlineData(nameof(CreateInquiryDto.FirstName), "   ")]
    [InlineData(nameof(CreateInquiryDto.LastName), "")]
    [InlineData(nameof(CreateInquiryDto.LastName), "   ")]
    [InlineData(nameof(CreateInquiryDto.Email), "")]
    [InlineData(nameof(CreateInquiryDto.Email), "   ")]
    [InlineData(nameof(CreateInquiryDto.CourseName), "")]
    [InlineData(nameof(CreateInquiryDto.CourseName), "   ")]
    [Trait("CaseId", "UT-VAL-003")]
    public void Empty_or_whitespace_only_required_field_is_rejected(string field, string value)
        => AssertOnlyFieldFlagged(Validate(SyntheticInquiry.With(field, value)), field);

    [Theory]
    [InlineData(nameof(CreateInquiryDto.Phone))]
    [InlineData(nameof(CreateInquiryDto.PreferredLocation))]
    [InlineData(nameof(CreateInquiryDto.Message))]
    [Trait("CaseId", "UT-VAL-004")]
    public void Optional_fields_accept_null_empty_and_whitespace_and_preserve_them(string field)
    {
        foreach (var value in new string?[] { null, "", "   ", "  x  " })
        {
            var dto = SyntheticInquiry.With(field, value);

            Assert.Empty(Validate(dto));
            Assert.Equal(value, SyntheticInquiry.Value(dto, field));
        }
    }

    [Fact]
    [Trait("CaseId", "UT-VAL-005")]
    public void Maximum_lengths_pass_for_ASCII_and_for_surrogate_pairs()
    {
        Assert.Empty(Validate(SyntheticInquiry.With(nameof(CreateInquiryDto.FirstName), new string('a', 100))));
        Assert.Empty(Validate(SyntheticInquiry.With(nameof(CreateInquiryDto.FirstName), Pairs(50))));
        Assert.Empty(Validate(SyntheticInquiry.With(nameof(CreateInquiryDto.LastName), new string('b', 100))));
        Assert.Empty(Validate(SyntheticInquiry.With(nameof(CreateInquiryDto.LastName), Pairs(50))));
        Assert.Empty(Validate(SyntheticInquiry.With(nameof(CreateInquiryDto.Email), AsciiEmail(254))));
        Assert.Empty(Validate(SyntheticInquiry.With(nameof(CreateInquiryDto.Email), PairsEmail(254))));
        Assert.Empty(Validate(SyntheticInquiry.With(nameof(CreateInquiryDto.Phone), new string('5', 50))));
        Assert.Empty(Validate(SyntheticInquiry.With(nameof(CreateInquiryDto.Phone), Pairs(25))));
        Assert.Empty(Validate(SyntheticInquiry.With(nameof(CreateInquiryDto.CourseName), new string('c', 200))));
        Assert.Empty(Validate(SyntheticInquiry.With(nameof(CreateInquiryDto.CourseName), Pairs(100))));
        Assert.Empty(Validate(SyntheticInquiry.With(nameof(CreateInquiryDto.PreferredLocation), new string('l', 200))));
        Assert.Empty(Validate(SyntheticInquiry.With(nameof(CreateInquiryDto.PreferredLocation), Pairs(100))));
        Assert.Empty(Validate(SyntheticInquiry.With(nameof(CreateInquiryDto.Message), new string('m', 4000))));
        Assert.Empty(Validate(SyntheticInquiry.With(nameof(CreateInquiryDto.Message), Pairs(2000))));
    }

    [Fact]
    [Trait("CaseId", "UT-VAL-006")]
    public void One_UTF16_unit_over_the_maximum_is_rejected_and_never_truncated()
    {
        var variants = new (string Field, string Value)[]
        {
            (nameof(CreateInquiryDto.FirstName), new string('a', 101)),
            (nameof(CreateInquiryDto.FirstName), Pairs(51)),
            (nameof(CreateInquiryDto.LastName), new string('b', 101)),
            (nameof(CreateInquiryDto.LastName), Pairs(51)),
            (nameof(CreateInquiryDto.Email), AsciiEmail(255)),
            (nameof(CreateInquiryDto.Email), PairsEmail(255, "@bb.c")),
            (nameof(CreateInquiryDto.Phone), new string('5', 51)),
            (nameof(CreateInquiryDto.Phone), Pairs(26)),
            (nameof(CreateInquiryDto.CourseName), new string('c', 201)),
            (nameof(CreateInquiryDto.CourseName), Pairs(101)),
            (nameof(CreateInquiryDto.PreferredLocation), new string('l', 201)),
            (nameof(CreateInquiryDto.PreferredLocation), Pairs(101)),
            (nameof(CreateInquiryDto.Message), new string('m', 4001)),
            (nameof(CreateInquiryDto.Message), Pairs(2001)),
        };

        foreach (var (field, value) in variants)
        {
            var dto = SyntheticInquiry.With(field, value);

            AssertOnlyFieldFlagged(Validate(dto), field);
            Assert.Equal(value, SyntheticInquiry.Value(dto, field));
        }
    }

    [Theory]
    [InlineData("a.b+c@sub.domain.co")]
    [InlineData("A.B@EXAMPLE.COM")]
    [InlineData("12345@example.com")]
    [InlineData("a@b.cc")]
    // Actual .NET [EmailAddress] semantics accept anything with exactly one '@'
    // that is neither first nor last; C1 selects that framework check instead of
    // a stricter hand-written parser, so these two catalog-declared "invalid"
    // values are in fact accepted.
    [InlineData("a@b")]
    [InlineData("space s@x.com")]
    [Trait("CaseId", "UT-VAL-007")]
    public void Framework_valid_emails_pass_the_format_check(string email)
        => Assert.Empty(Validate(SyntheticInquiry.With(nameof(CreateInquiryDto.Email), email)));

    [Theory]
    [InlineData("no-at-symbol")]
    [InlineData("two@@ats.com")]
    [InlineData("@leading.com")]
    [InlineData("trailing@")]
    [Trait("CaseId", "UT-VAL-008")]
    public void Emails_without_exactly_one_inner_at_sign_are_rejected(string email)
        => AssertOnlyFieldFlagged(Validate(SyntheticInquiry.With(nameof(CreateInquiryDto.Email), email)),
            nameof(CreateInquiryDto.Email));

    private static List<ValidationResult> Validate(CreateInquiryDto dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    private static void AssertOnlyFieldFlagged(IReadOnlyCollection<ValidationResult> errors, string field)
    {
        Assert.NotEmpty(errors);
        Assert.Equal(new[] { field }, errors.SelectMany(error => error.MemberNames).Distinct().ToArray());
    }

    /// <summary>One Unicode character (U+1D11E) repeated: two UTF-16 code units each.</summary>
    private static string Pairs(int pairs) => string.Concat(Enumerable.Repeat("𝄞", pairs));

    private static string AsciiEmail(int units, string suffix = "@b.c") =>
        new string('a', units - suffix.Length) + suffix;

    private static string PairsEmail(int units, string suffix = "@b.c") =>
        Pairs((units - suffix.Length) / 2) + suffix;
}
