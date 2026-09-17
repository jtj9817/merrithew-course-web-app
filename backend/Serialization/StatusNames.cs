using CourseInquiryDashboard.Models;

namespace CourseInquiryDashboard.Serialization;

/// <summary>
/// The explicit, name-only status vocabulary of contract C2.
/// </summary>
/// <remarks>
/// Names are matched case-insensitively after trimming surrounding whitespace.
/// Only the five defined names are accepted: numeric strings (including
/// <c>"0"</c>), comma-separated names, empty/whitespace input, and unknown
/// words are rejected — generic enum parsing would coerce several of those
/// (e.g. <c>"New,Closed"</c> parses as <see cref="Status.Closed"/>). Output
/// always uses the contract casing.
/// </remarks>
public static class StatusNames
{
    /// <summary>Canonical names in enum declaration order.</summary>
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(Enum.GetNames<Status>());

    /// <summary>The canonical names as a comma-separated list, for human-readable messages.</summary>
    public static string CommaSeparated { get; } = string.Join(", ", All);

    private static readonly Dictionary<string, Status> ByName =
        All.ToDictionary(name => name, Enum.Parse<Status>, StringComparer.OrdinalIgnoreCase);

    /// <summary>True when the value is one of the five defined members.</summary>
    public static bool IsDefined(Status status) => Enum.IsDefined(status);

    /// <summary>Canonical contract name for a defined status; throws otherwise.</summary>
    public static string ToContractName(Status status)
    {
        if (!IsDefined(status))
            throw new InvalidOperationException(
                $"Status value '{(int)status}' is not defined and has no contract name.");

        return All[(int)status];
    }

    /// <summary>
    /// Parses a status name exactly as the wire contract accepts it
    /// (case-insensitive, surrounding whitespace tolerated).
    /// </summary>
    public static bool TryParse(string? name, out Status status)
    {
        status = default;
        return ByName.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name.AsSpan().Trim(), out status);
    }
}
