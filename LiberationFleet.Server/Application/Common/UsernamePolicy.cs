using System.Text.RegularExpressions;

namespace LiberationFleet.Server.Application.Common;

/// <summary>
/// Central username rules shared by registration and profile updates.
/// EF Core parameterizes all queries, so this is defense-in-depth plus
/// reserved-name protection rather than the sole SQL-injection guard.
/// </summary>
public static partial class UsernamePolicy
{
    public const int MinLength = 3;
    public const int MaxLength = 30;

    public const string PatternDescription = "letters and numbers only";

    // Substrings that must never appear (case-insensitive). The alphanumeric
    // pattern already blocks spaces/punctuation, so multi-word injections like
    // "drop table" or "ignore all prior instructions" cannot pass regardless.
    private static readonly string[] ReservedSubstrings =
    {
        "admin",
        "administrator",
        "moderator",
        "root",
        "system",
        "support",
        "owner",
        "sysadmin",
        "superuser",
        "sql",
        "select",
        "insert",
        "update",
        "delete",
        "drop",
        "truncate",
        "null"
    };

    [GeneratedRegex("^[A-Za-z0-9]+$")]
    private static partial Regex AllowedPatternRegex();

    public static bool MatchesPattern(string? username) =>
        !string.IsNullOrEmpty(username) && AllowedPatternRegex().IsMatch(username);

    public static bool IsAllowed(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        var lowered = username.ToLowerInvariant();
        foreach (var reserved in ReservedSubstrings)
        {
            if (lowered.Contains(reserved))
            {
                return false;
            }
        }

        return true;
    }
}
