using System.Text.Json;
using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Models.Dtos;
using CourseInquiryDashboard.Serialization;

namespace CourseInquiryDashboard.Tests.Unit;

/// <summary>
/// UT-VAL-009/010: contract C2 status names. The same explicit name-only rule
/// serves the JSON status update, the optional list filter, and canonical output
/// names — numeric, composite, unknown, and empty inputs are never coerced and
/// never default to <see cref="Status.New"/>.
/// </summary>
[Trait("Category", "Unit")]
public class StatusParsingTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new StatusJsonConverter() },
    };

    [Theory]
    [InlineData("New", Status.New)]
    [InlineData("Contacted", Status.Contacted)]
    [InlineData("Pending", Status.Pending)]
    [InlineData("Registered", Status.Registered)]
    [InlineData("Closed", Status.Closed)]
    [InlineData("new", Status.New)]
    [InlineData("CONTACTED", Status.Contacted)]
    [InlineData("  pEnDiNg  ", Status.Pending)]
    [InlineData("\tregistered\r\n", Status.Registered)]
    [Trait("CaseId", "UT-VAL-009")]
    public void Status_names_parse_case_insensitively_with_surrounding_whitespace(string input, Status expected)
    {
        Assert.True(StatusNames.TryParse(input, out var parsed));
        Assert.Equal(expected, parsed);
        Assert.Equal(expected.ToString(), StatusNames.ToContractName(parsed));
    }

    [Theory]
    [InlineData("New")]
    [InlineData("Contacted")]
    [InlineData("Pending")]
    [InlineData("Registered")]
    [InlineData("Closed")]
    [Trait("CaseId", "UT-VAL-009")]
    public void Canonical_names_are_stable_contract_casing(string name)
    {
        Assert.Contains(name, StatusNames.All);
        Assert.True(StatusNames.TryParse(name.ToUpperInvariant(), out var parsed));
        Assert.Equal(name, StatusNames.ToContractName(parsed));
    }

    [Fact]
    [Trait("CaseId", "UT-VAL-009")]
    public void Json_binding_accepts_the_same_names_and_writes_canonical_casing()
    {
        var dto = JsonSerializer.Deserialize<UpdateStatusDto>("{\"status\":\"  pEnDiNg  \"}", WebJson);
        Assert.Equal(Status.Pending, dto!.Status);

        Assert.Equal("{\"status\":\"Pending\"}", JsonSerializer.Serialize(dto, WebJson));
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("New,Closed")]
    [InlineData("New, Closed")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("-1")]
    [InlineData("99")]
    [InlineData("1.0")]
    [InlineData("true")]
    [InlineData("ContactedPending")]
    [InlineData(null)]
    [Trait("CaseId", "UT-VAL-010")]
    public void Non_name_inputs_are_never_coerced_to_a_status(string? input)
    {
        Assert.False(StatusNames.TryParse(input, out var parsed));
        Assert.Equal(default, parsed);
    }

    [Theory]
    [InlineData("{\"status\":0}")]
    [InlineData("{\"status\":1}")]
    [InlineData("{\"status\":\"0\"}")]
    [InlineData("{\"status\":\"1\"}")]
    [InlineData("{\"status\":true}")]
    [InlineData("{\"status\":[]}")]
    [InlineData("{\"status\":{}}")]
    [InlineData("{\"status\":\"New,Closed\"}")]
    [InlineData("{\"status\":\"Draft\"}")]
    [Trait("CaseId", "UT-VAL-010")]
    public void Json_values_outside_the_name_only_rule_are_rejected_without_echoing_them(string json)
    {
        var exception = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateStatusDto>(json, WebJson));

        Assert.DoesNotContain("Draft", exception.Message);
        Assert.DoesNotContain("New,Closed", exception.Message);
    }
}
