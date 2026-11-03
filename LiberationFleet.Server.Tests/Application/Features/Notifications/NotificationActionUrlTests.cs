using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Tests.Application.Features.Notifications;

public class NotificationActionUrlTests
{
    [Fact]
    public void MatchesPrefix_RequiresAPathBoundary()
    {
        var match = NotificationActionUrl.MatchesPrefix("/app/friends/messages/1").Compile();

        match(Row("/app/friends/messages/1")).Should().BeTrue();
        match(Row("/app/friends/messages/1?x=1")).Should().BeTrue();
        match(Row("/app/friends/messages/12")).Should().BeFalse();
        match(Row("/app/friends/messages/10")).Should().BeFalse();
    }

    [Theory]
    [InlineData("/app/crew/chats/5", "/app/crew/chats/5?messageId=9", true)]
    [InlineData("/app/crew/chats/5", "/app/crew/chats/50?messageId=9", false)]
    [InlineData("/app/friends/accepted", "/app/friends/messages/3", false)]
    public void MatchesPrefix_CoversChatAndListPaths(string prefix, string actionUrl, bool expected)
    {
        var match = NotificationActionUrl.MatchesPrefix(prefix).Compile();
        match(Row(actionUrl)).Should().Be(expected);
    }

    private static Notification Row(string actionUrl) => new() { ActionUrl = actionUrl };
}
