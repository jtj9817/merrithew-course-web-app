using System.Text.Json;
using System.Text.Json.Serialization;
using CourseInquiryDashboard.Models;

namespace CourseInquiryDashboard.Serialization;

/// <summary>
/// Serializes <see cref="Status"/> as its canonical C2 name and deserializes it
/// only from a JSON string holding one of the five defined names. Register it
/// globally for MVC (<c>AddControllers().AddJsonOptions(...)</c>) so request
/// binding and response serialization share the name-only rule; the framework
/// handles <see cref="Nullable{T}"/> members itself, so an explicit JSON
/// <c>null</c> stays null and is left to <c>[Required]</c> validation.
/// </summary>
/// <remarks>
/// Error messages never echo the submitted value: the message would otherwise
/// reach the 400 ProblemDetails <c>errors</c> dictionary (C3).
/// </remarks>
public sealed class StatusJsonConverter : JsonConverter<Status>
{
    private static readonly string InvalidStatusMessage =
        $"The status must be one of the defined names ({StatusNames.CommaSeparated}).";

    public override Status Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || !StatusNames.TryParse(reader.GetString(), out var status))
            throw new JsonException(InvalidStatusMessage);

        return status;
    }

    public override void Write(Utf8JsonWriter writer, Status value, JsonSerializerOptions options)
        => writer.WriteStringValue(StatusNames.ToContractName(value));
}
