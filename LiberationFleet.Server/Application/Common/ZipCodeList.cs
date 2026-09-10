using System.Text.RegularExpressions;

namespace LiberationFleet.Server.Application.Common;

/// <summary>
/// Normalize and compare international postal-code allowlists
/// (comma-separated serialization for proposals). Codes are alphanumeric;
/// matching is always scoped by country elsewhere.
/// </summary>
public static partial class ZipCodeList
{
    public const int MaxCount = 200;
    public const int MinLength = 2;
    public const int MaxLength = 12;

    [GeneratedRegex(@"^[A-Z0-9]{2,12}$")]
    private static partial Regex NormalizedPostalRegex();

    public static bool IsValidZip(string? zip) => NormalizeZip(zip) is not null;

    /// <summary>
    /// Uppercase and strip spaces/hyphens so "SW1A 1AA" and "sw1a-1aa" compare equal.
    /// </summary>
    public static string? NormalizeZip(string? zip)
    {
        if (string.IsNullOrWhiteSpace(zip))
        {
            return null;
        }

        var chars = zip
            .Trim()
            .ToUpperInvariant()
            .Where(c => c is not (' ' or '-' or '\t'))
            .ToArray();

        if (chars.Length is < MinLength or > MaxLength)
        {
            return null;
        }

        var normalized = new string(chars);
        return NormalizedPostalRegex().IsMatch(normalized) ? normalized : null;
    }

    /// <summary>Dedupe, validate, and sort. Invalid entries are skipped.</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? zips)
    {
        if (zips is null)
        {
            return Array.Empty<string>();
        }

        return zips
            .Select(NormalizeZip)
            .Where(z => z is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(z => z, StringComparer.Ordinal)
            .Take(MaxCount)
            .ToList();
    }

    public static string Serialize(IEnumerable<string>? zips) =>
        string.Join(',', Normalize(zips));

    public static IReadOnlyList<string> Deserialize(string? serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return Array.Empty<string>();
        }

        return Normalize(serialized.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public static bool SequenceEqual(IEnumerable<string>? a, IEnumerable<string>? b) =>
        Serialize(a) == Serialize(b);

    public static bool Contains(IEnumerable<string>? allowlist, string? viewerZip)
    {
        var zip = NormalizeZip(viewerZip);
        if (zip is null)
        {
            return false;
        }

        return Normalize(allowlist).Contains(zip, StringComparer.Ordinal);
    }

    /// <summary>
    /// Empty allowlist means no geographic restriction (offerings).
    /// Non-empty requires matching country and a listed postal code.
    /// </summary>
    public static bool IsAccessible(
        string? entityCountryCode,
        IEnumerable<string>? allowlist,
        string? viewerCountryCode,
        string? viewerZip)
    {
        var list = Normalize(allowlist);
        if (list.Count == 0)
        {
            return true;
        }

        if (!CountryCodes.Matches(entityCountryCode, viewerCountryCode))
        {
            return false;
        }

        return Contains(list, viewerZip);
    }

    /// <summary>Local crew/fleet match: same country and postal in allowlist.</summary>
    public static bool MatchesLocal(
        string? entityCountryCode,
        IEnumerable<string>? allowlist,
        string? viewerCountryCode,
        string? viewerZip) =>
        CountryCodes.Matches(entityCountryCode, viewerCountryCode)
        && Contains(allowlist, viewerZip);

    public static string FormatDisplay(IEnumerable<string>? zips)
    {
        var list = Normalize(zips);
        return list.Count == 0 ? "(none)" : string.Join(", ", list);
    }

    public static bool TryValidateLocalRequired(
        string? countryCode,
        IEnumerable<string>? zips,
        out string? normalizedCountry,
        out IReadOnlyList<string> normalizedZips,
        out string error)
    {
        normalizedCountry = CountryCodes.Normalize(countryCode);
        normalizedZips = Normalize(zips);

        if (normalizedCountry is null)
        {
            error = "Local groups require a valid country.";
            return false;
        }

        if (normalizedZips.Count == 0)
        {
            error = "Local groups require at least one valid postal code.";
            return false;
        }

        if (zips is not null)
        {
            var raw = zips.Where(z => !string.IsNullOrWhiteSpace(z)).Select(z => z.Trim()).ToList();
            if (raw.Any(z => !IsValidZip(z)))
            {
                error = "Each postal code must be 2–12 letters or digits (spaces and hyphens allowed).";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public static bool TryValidateOptional(
        string? countryCode,
        IEnumerable<string>? zips,
        out string? normalizedCountry,
        out IReadOnlyList<string> normalizedZips,
        out string error)
    {
        if (zips is not null)
        {
            var raw = zips.Where(z => !string.IsNullOrWhiteSpace(z)).Select(z => z.Trim()).ToList();
            if (raw.Any(z => !IsValidZip(z)))
            {
                normalizedCountry = null;
                normalizedZips = Array.Empty<string>();
                error = "Each postal code must be 2–12 letters or digits (spaces and hyphens allowed).";
                return false;
            }
        }

        normalizedZips = Normalize(zips);
        if (normalizedZips.Count == 0)
        {
            normalizedCountry = null;
            error = string.Empty;
            return true;
        }

        normalizedCountry = CountryCodes.Normalize(countryCode);
        if (normalizedCountry is null)
        {
            error = "Select a country when restricting by postal code.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
