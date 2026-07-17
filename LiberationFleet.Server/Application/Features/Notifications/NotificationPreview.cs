using System.Text.RegularExpressions;

namespace LiberationFleet.Server.Application.Features.Notifications;

public static class NotificationPreview
{
    public const int MaxLength = 200;

    /// <summary>Normalize whitespace and truncate to <see cref="MaxLength"/> characters.</summary>
    public static string Truncate(string? text, int maxLength = MaxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = Regex.Replace(text.Trim(), @"\s+", " ");
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..(maxLength - 1)].TrimEnd() + "…";
    }

    /// <summary>Use truncated preview when present; otherwise the fallback body.</summary>
    public static string BodyOrFallback(string? preview, string fallback) =>
        Truncate(preview) is { Length: > 0 } body ? body : fallback;
}
