using System.Net;
using System.Text.Json;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Push;

namespace LiberationFleet.Server.Tests.Infrastructure.Push;

public class PushPayloadsTests
{
    private static NotificationDto Sample() => new()
    {
        Id = 42,
        Kind = NotificationKind.NewDirectMessage,
        Title = "New message",
        Body = "Hello from a crewmate",
        ActionUrl = "/app/friends/dm/9"
    };

    [Fact]
    public void FcmMessage_IsAnAlertWithChannelAndBadge()
    {
        var json = JsonSerializer.Serialize(PushPayloads.FcmMessage("fcm-token", Sample(), 3));
        using var doc = JsonDocument.Parse(json);
        var message = doc.RootElement.GetProperty("message");

        message.GetProperty("token").GetString().Should().Be("fcm-token");
        message.GetProperty("notification").GetProperty("title").GetString().Should().Be("New message");
        message.GetProperty("notification").GetProperty("body").GetString().Should().Be("Hello from a crewmate");
        message.GetProperty("data").GetProperty("actionUrl").GetString().Should().Be("/app/friends/dm/9");

        var android = message.GetProperty("android");
        android.GetProperty("priority").GetString().Should().Be("HIGH");
        var tray = android.GetProperty("notification");
        tray.GetProperty("channelId").GetString().Should().Be(PushPayloads.AndroidChannelId);
        tray.GetProperty("notificationCount").GetInt32().Should().Be(3);
        tray.GetProperty("title").GetString().Should().Be("New message");
    }

    [Fact]
    public void ApnsMessage_IsAnAlertWithBadge()
    {
        var json = JsonSerializer.Serialize(PushPayloads.ApnsMessage(Sample(), 4));
        using var doc = JsonDocument.Parse(json);
        var aps = doc.RootElement.GetProperty("aps");

        aps.GetProperty("alert").GetProperty("title").GetString().Should().Be("New message");
        aps.GetProperty("alert").GetProperty("body").GetString().Should().Be("Hello from a crewmate");
        aps.GetProperty("sound").GetString().Should().Be("default");
        aps.GetProperty("badge").GetInt32().Should().Be(4);
        doc.RootElement.GetProperty("actionUrl").GetString().Should().Be("/app/friends/dm/9");
    }

    [Fact]
    public void CreateApnsRequest_UsesHttp2AlertHeaders()
    {
        using var request = FcmApnsPushNotificationSender.CreateApnsRequest(
            "jwt",
            "com.liberationfleet.app",
            "abc123",
            "{}",
            sandbox: false);

        request.RequestUri.Should().Be(new Uri("https://api.push.apple.com/3/device/abc123"));
        request.Version.Should().Be(HttpVersion.Version20);
        request.VersionPolicy.Should().Be(HttpVersionPolicy.RequestVersionOrHigher);
        request.Headers.GetValues("apns-topic").Should().Contain("com.liberationfleet.app");
        request.Headers.GetValues("apns-push-type").Should().Contain("alert");
        request.Headers.GetValues("apns-priority").Should().Contain("10");
    }
}
