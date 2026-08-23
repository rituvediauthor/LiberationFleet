namespace LiberationFleet.Server.Domain;

/// <summary>
/// Stable keys stored in <see cref="Entities.User.IdentityGroups"/> (comma-separated).
/// Labels are client-facing; keep keys stable for stored data.
/// </summary>
public static class IdentityGroupKeys
{
    public const string PhysicallyDisfigured = "PhysicallyDisfigured";
    public const string PhysicallyDisabledOrUnaccommodated = "PhysicallyDisabledOrUnaccommodated";
    public const string CognitivelyDisabledOrUnaccommodated = "CognitivelyDisabledOrUnaccommodated";
    public const string Bipoc = "Bipoc";
    public const string Woman = "Woman";
    public const string NotHeterosexual = "NotHeterosexual";
    public const string Trans = "Trans";
    public const string Intersex = "Intersex";
    public const string UnhousedOrHousingInsecure = "UnhousedOrHousingInsecure";
    public const string ImmigrantOrRefugee = "ImmigrantOrRefugee";
    public const string ReligiousOrAreligiousMinority = "ReligiousOrAreligiousMinority";
    public const string Neurodivergent = "Neurodivergent";
    public const string VisiblyOrAudiblyDisabled = "VisiblyOrAudiblyDisabled";
    public const string OtherTargetedMinority = "OtherTargetedMinority";

    public static readonly string[] All =
    [
        PhysicallyDisfigured,
        PhysicallyDisabledOrUnaccommodated,
        CognitivelyDisabledOrUnaccommodated,
        Bipoc,
        Woman,
        NotHeterosexual,
        Trans,
        Intersex,
        UnhousedOrHousingInsecure,
        ImmigrantOrRefugee,
        ReligiousOrAreligiousMinority,
        Neurodivergent,
        VisiblyOrAudiblyDisabled,
        OtherTargetedMinority
    ];

    private static readonly HashSet<string> Allowed = new(All, StringComparer.Ordinal);

    public static bool IsValid(string? key) =>
        !string.IsNullOrWhiteSpace(key) && Allowed.Contains(key.Trim());

    public static bool AreValid(IEnumerable<string>? keys) =>
        keys is null || keys.All(IsValid);

    public static IReadOnlyList<string> Parse(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return Array.Empty<string>();
        }

        return stored
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsValid)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => Array.IndexOf(All, k))
            .ToList();
    }

    public static string? Serialize(IEnumerable<string>? keys)
    {
        if (keys is null)
        {
            return null;
        }

        var normalized = keys
            .Where(IsValid)
            .Select(k => k.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => Array.IndexOf(All, k))
            .ToList();

        return normalized.Count == 0 ? null : string.Join(',', normalized);
    }
}
