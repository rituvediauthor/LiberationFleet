namespace LiberationFleet.Server.Domain;

/// <summary>
/// Stable keys stored in <see cref="Entities.User.IdentityGroups"/> (comma-separated).
/// </summary>
public static class IdentityGroupKeys
{
    public const string NonWhite = "NonWhite";
    public const string Woman = "Woman";
    public const string Lgbtqia = "Lgbtqia";
    public const string NotConventionallyAttractive = "NotConventionallyAttractive";
    public const string Homeless = "Homeless";
    public const string VisiblyOrAudiblyDisabled = "VisiblyOrAudiblyDisabled";

    public static readonly string[] All =
    [
        NonWhite,
        Woman,
        Lgbtqia,
        NotConventionallyAttractive,
        Homeless,
        VisiblyOrAudiblyDisabled
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
