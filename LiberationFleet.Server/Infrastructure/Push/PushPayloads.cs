using LiberationFleet.Server.Application.Features.Notifications.Contracts;

namespace LiberationFleet.Server.Infrastructure.Push;

/// <summary>
/// FCM HTTP v1 and APNs alert bodies. Channel id must match
/// <c>PushNotificationRegistrationService.ANDROID_PUSH_CHANNEL_ID</c>.
/// </summary>
public static class PushPayloads
{
    public const string AndroidChannelId = "liberation_fleet_alerts";

    public static Dictionary<string, object> FcmMessage(
        string token,
        NotificationDto notification,
        int? badgeCount)
    {
        var androidNotification = new Dictionary<string, object>
        {
            ["title"] = Truncate(notification.Title, 100),
            ["body"] = Truncate(notification.Body, 200),
            ["channelId"] = AndroidChannelId,
            ["icon"] = "ic_stat_notify",
            ["color"] = "#0B1220",
            ["sound"] = "default",
            ["defaultSound"] = true,
            ["notificationPriority"] = "PRIORITY_HIGH",
            ["visibility"] = "PUBLIC"
        };
        if (badgeCount is int badge)
        {
            androidNotification["notificationCount"] = badge;
        }

        return new Dictionary<string, object>
        {
            ["message"] = new Dictionary<string, object>
            {
                ["token"] = token,
                ["notification"] = new Dictionary<string, string>
                {
                    ["title"] = Truncate(notification.Title, 100),
                    ["body"] = Truncate(notification.Body, 200)
                },
                ["data"] = new Dictionary<string, string>
                {
                    ["notificationId"] = notification.Id.ToString(),
                    ["actionUrl"] = notification.ActionUrl ?? string.Empty,
                    ["kind"] = notification.Kind.ToString()
                },
                ["android"] = new Dictionary<string, object>
                {
                    ["priority"] = "HIGH",
                    ["notification"] = androidNotification
                }
            }
        };
    }

    public static Dictionary<string, object?> ApnsMessage(
        NotificationDto notification,
        int? badgeCount)
    {
        var aps = new Dictionary<string, object?>
        {
            ["alert"] = new Dictionary<string, string>
            {
                ["title"] = Truncate(notification.Title, 100),
                ["body"] = Truncate(notification.Body, 200)
            },
            ["sound"] = "default",
            ["interruption-level"] = "active"
        };
        if (badgeCount is int badge)
        {
            aps["badge"] = badge;
        }

        return new Dictionary<string, object?>
        {
            ["aps"] = aps,
            ["notificationId"] = notification.Id,
            ["actionUrl"] = notification.ActionUrl ?? string.Empty,
            ["kind"] = notification.Kind.ToString()
        };
    }

    public static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
