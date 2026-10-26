using FluentAssertions;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Notifications;

public class NotificationServiceTests
{
    [Fact]
    public async Task NotifyCrewAsync_CanOmitActorIdentityWhileStillExcludingAuthor()
    {
        var created = new List<Notification>();
        var notificationRepository = new Mock<INotificationRepository>(MockBehavior.Loose);
        notificationRepository
            .Setup(r => r.GetCrewMemberUserIdsAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([2, 3]);
        notificationRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Notification>, CancellationToken>((items, _) => created.AddRange(items))
            .Returns(Task.CompletedTask);
        notificationRepository
            .Setup(r => r.GetUnreadCountAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var service = HandlerTestFixture.CreateNotificationService(notificationRepository: notificationRepository);

        await service.NotifyCrewAsync(
            7,
            NotificationKind.NewProposal,
            "Anonymous submitted a proposal",
            "Preview",
            "/app/crew/proposals",
            relatedEntityId: 12,
            excludeUserId: 1,
            omitActorIdentity: true,
            cancellationToken: CancellationToken.None);

        created.Select(n => n.UserId).Should().BeEquivalentTo([2, 3]);
        created.Select(n => n.ActorUserId).Should().OnlyContain(actorUserId => actorUserId == null);
    }

    [Fact]
    public async Task NotifyCrewAsync_SkipsStandardNotificationWhenMentionIsEnabled()
    {
        var created = new List<Notification>();
        var notificationRepository = new Mock<INotificationRepository>(MockBehavior.Loose);
        notificationRepository
            .Setup(r => r.GetCrewMemberUserIdsAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([2, 3]);
        notificationRepository
            .Setup(r => r.IsKindEnabledAsync(2, NotificationKind.Mention, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        notificationRepository
            .Setup(r => r.IsKindEnabledAsync(3, NotificationKind.Mention, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        notificationRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Notification>, CancellationToken>((items, _) => created.AddRange(items))
            .Returns(Task.CompletedTask);
        notificationRepository
            .Setup(r => r.GetUnreadCountAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var service = HandlerTestFixture.CreateNotificationService(notificationRepository: notificationRepository);

        await service.NotifyCrewAsync(
            7,
            NotificationKind.NewChatMessage,
            "Ada posted in General",
            "hello",
            "/app/crew/chats/5",
            excludeUserId: 1,
            mentionedUserIds: [2, 3],
            cancellationToken: CancellationToken.None);

        created.Select(n => n.UserId).Should().Equal(3);
        created.Should().OnlyContain(n => n.Kind == NotificationKind.NewChatMessage);
    }
}
