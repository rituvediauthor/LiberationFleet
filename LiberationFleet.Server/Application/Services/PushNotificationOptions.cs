namespace LiberationFleet.Server.Application.Services;

public class PushNotificationOptions
{
    public const string SectionName = "Push";

    /// <summary>Firebase project id (FCM HTTP v1).</summary>
    public string FcmProjectId { get; set; } = string.Empty;

    /// <summary>Full Google service-account JSON (private_key, client_email, …).</summary>
    public string FcmServiceAccountJson { get; set; } = string.Empty;

    /// <summary>Contents of the APNs AuthKey_XXXXX.p8 file.</summary>
    public string ApnsKeyP8 { get; set; } = string.Empty;

    public string ApnsKeyId { get; set; } = string.Empty;
    public string ApnsTeamId { get; set; } = string.Empty;
    public string ApnsBundleId { get; set; } = "com.liberationfleet.app";

    /// <summary>Use api.sandbox.push.apple.com when true (TestFlight / debug).</summary>
    public bool ApnsUseSandbox { get; set; }

    public bool IsFcmConfigured =>
        !string.IsNullOrWhiteSpace(FcmProjectId)
        && !string.IsNullOrWhiteSpace(FcmServiceAccountJson)
        && !FcmServiceAccountJson.Contains("change-me", StringComparison.OrdinalIgnoreCase);

    public bool IsApnsConfigured =>
        !string.IsNullOrWhiteSpace(ApnsKeyP8)
        && !string.IsNullOrWhiteSpace(ApnsKeyId)
        && !string.IsNullOrWhiteSpace(ApnsTeamId)
        && !ApnsKeyP8.Contains("change-me", StringComparison.OrdinalIgnoreCase);
}
