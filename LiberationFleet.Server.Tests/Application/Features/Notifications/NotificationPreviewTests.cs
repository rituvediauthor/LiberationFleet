using LiberationFleet.Server.Application.Features.Notifications;

namespace LiberationFleet.Server.Tests.Application.Features.Notifications;

public class NotificationPreviewTests
{
    [Fact]
    public void Truncate_WhenLongerThanMax_EndsWithThreeDots()
    {
        var text = new string('a', 220);

        var truncated = NotificationPreview.Truncate(text);

        truncated.Should().EndWith("...");
        truncated.Length.Should().Be(NotificationPreview.MaxLength);
        truncated[..197].Should().Be(new string('a', 197));
    }

    [Fact]
    public void BodyOrFallback_UsesPreviewWhenPresent()
    {
        NotificationCopy.PreviewOr("Hello from chat", "Open the chat to read the message.")
            .Should().Be("Hello from chat");
    }

    [Fact]
    public void BodyOrFallback_UsesFallbackWhenPreviewMissing()
    {
        NotificationCopy.PreviewOr("  ", "Open the chat to read the message.")
            .Should().Be("Open the chat to read the message.");
    }
}
