using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Models.Dtos;
using CourseInquiryDashboard.Serialization;

namespace CourseInquiryDashboard.Tests.Unit;

/// <summary>
/// UT-VAL-011 and the DTO half of UT-VAL-012: an omitted or null status is
/// invalid (never the <see cref="Status.New"/> zero default), and an
/// out-of-range cast is rejected by the defined-membership guard rather than
/// being persisted when HTTP is bypassed.
/// </summary>
[Trait("Category", "Unit")]
public class UpdateStatusDtoValidationTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new StatusJsonConverter() },
    };

    [Fact]
    [Trait("CaseId", "UT-VAL-011")]
    public void Omitted_and_null_status_are_invalid_and_never_default_to_New()
    {
        var omitted = new UpdateStatusDto();
        Assert.Null(omitted.Status);
        AssertOnlyStatusFlagged(omitted);

        var explicitNull = new UpdateStatusDto { Status = null };
        Assert.Null(explicitNull.Status);
        AssertOnlyStatusFlagged(explicitNull);

        // The wire shape binds both JSON spellings to null, not to the enum's zero value.
        Assert.Null(JsonSerializer.Deserialize<UpdateStatusDto>("{}", WebJson)!.Status);
        Assert.Null(JsonSerializer.Deserialize<UpdateStatusDto>("{\"status\":null}", WebJson)!.Status);
    }

    [Theory]
    [InlineData(Status.New)]
    [InlineData(Status.Contacted)]
    [InlineData(Status.Pending)]
    [InlineData(Status.Registered)]
    [InlineData(Status.Closed)]
    [Trait("CaseId", "UT-VAL-011")]
    public void Every_defined_status_passes_validation(Status status)
        => Assert.Empty(Validate(new UpdateStatusDto { Status = status }));

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    [InlineData(5)]
    [Trait("CaseId", "UT-VAL-012")]
    public void Undefined_enum_values_are_rejected_by_the_defined_status_guard(int raw)
    {
        var dto = new UpdateStatusDto { Status = (Status)raw };

        Assert.False(StatusNames.IsDefined(dto.Status!.Value));
        AssertOnlyStatusFlagged(dto);
        Assert.Equal((Status)raw, dto.Status); // validation never mutates or defaults the value
        Assert.Throws<InvalidOperationException>(() => StatusNames.ToContractName(dto.Status!.Value));
    }

    private static void AssertOnlyStatusFlagged(UpdateStatusDto dto)
    {
        var errors = Validate(dto);
        Assert.NotEmpty(errors);
        Assert.Equal(new[] { nameof(UpdateStatusDto.Status) },
            errors.SelectMany(error => error.MemberNames).Distinct().ToArray());
    }

    private static List<ValidationResult> Validate(UpdateStatusDto dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }
}
