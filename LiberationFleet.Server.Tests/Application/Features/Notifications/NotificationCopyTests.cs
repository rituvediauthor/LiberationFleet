using FluentAssertions;
using LiberationFleet.Server.Application.Features.Notifications;

namespace LiberationFleet.Server.Tests.Application.Features.Notifications;

public class NotificationCopyTests
{
    [Fact]
    public void MentionedYouIn_IncludesLocation()
    {
        NotificationCopy.MentionedYouIn("Ada", "General")
            .Should().Be("Ada mentioned you in General");
        NotificationCopy.MentionedYouIn("Ada", NotificationCopy.GiftLog)
            .Should().Be("Ada mentioned you in Gift log");
        NotificationCopy.MentionedYouIn("Ada", NotificationCopy.LibraryOfThings)
            .Should().Be("Ada mentioned you in Library of Things");
        NotificationCopy.MentionedYouIn("Ada", null)
            .Should().Be("Ada mentioned you");
    }
}
