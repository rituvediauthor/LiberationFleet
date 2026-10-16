using FluentAssertions;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Tests.Application.Features.Notifications;

public class NotificationActorPrivacyTests
{
    [Fact]
    public void Map_OmitsActorIdentityForAnonymousChatNotification()
    {
        var notification = new Notification
        {
            Id = 1,
            UserId = 2,
            Kind = NotificationKind.NewChatMessage,
            Title = "Anonymous posted in General",
            Body = "hello",
            ActionUrl = "/app/crew/chats/5?messageId=9",
            ActorUserId = 44
        };

        var dto = NotificationMapper.Map(notification);

        dto.ActorUserId.Should().BeNull();
    }

    [Fact]
    public void Map_KeepsActorIdentityForNamedChatNotification()
    {
        var notification = new Notification
        {
            Id = 1,
            UserId = 2,
            Kind = NotificationKind.NewChatMessage,
            Title = "Ada posted in General",
            Body = "hello",
            ActionUrl = "/app/crew/chats/5?messageId=9",
            ActorUserId = 44
        };

        var dto = NotificationMapper.Map(notification);

        dto.ActorUserId.Should().Be(44);
    }

    [Fact]
    public void Map_OmitsActorIdentityForProposalReplyNotification()
    {
        var notification = new Notification
        {
            Id = 1,
            UserId = 2,
            Kind = NotificationKind.NewProposalReply,
            Title = "River commented on your proposal",
            Body = "hello",
            ActionUrl = "/app/crew/proposals/4?commentId=2",
            ActorUserId = 44
        };

        var dto = NotificationMapper.Map(notification);

        dto.ActorUserId.Should().BeNull();
    }

    [Fact]
    public void Map_OmitsActorIdentityForProposalMentionNotification()
    {
        var notification = new Notification
        {
            Id = 1,
            UserId = 2,
            Kind = NotificationKind.Mention,
            Title = "River mentioned you",
            Body = "hello",
            ActionUrl = "/app/crew/proposals/4?commentId=2",
            ActorUserId = 44
        };

        var dto = NotificationMapper.Map(notification);

        dto.ActorUserId.Should().BeNull();
    }
}
