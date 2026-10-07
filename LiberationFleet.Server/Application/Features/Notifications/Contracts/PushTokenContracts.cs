using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Notifications.Contracts;

public class RegisterPushTokenRequest
{
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
}

public class UnregisterPushTokenRequest
{
    public string? Token { get; set; }
    /// <summary>When true, removes all tokens for the current user (logout all devices).</summary>
    public bool AllDevices { get; set; }
}

public static class PushPlatformParser
{
    public static bool TryParse(string? value, out PushPlatform platform)
    {
        platform = PushPlatform.Android;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "android":
            case "fcm":
                platform = PushPlatform.Android;
                return true;
            case "ios":
            case "apns":
                platform = PushPlatform.Ios;
                return true;
            default:
                return false;
        }
    }
}
