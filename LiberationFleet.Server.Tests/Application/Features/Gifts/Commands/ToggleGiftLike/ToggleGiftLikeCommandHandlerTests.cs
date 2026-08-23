using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Commands.ToggleGiftLike;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts.Commands.ToggleGiftLike;

public class ToggleGiftLikeCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenFirstLike_NotifiesGiverAndRecipient()
    {
        var actor = HandlerTestFixture.CreateUser(id: 3, username: "Actor");
        var giver = HandlerTestFixture.CreateUser(id: 1, username: "Giver");
        var recipient = HandlerTestFixture.CreateUser(id: 2, username: "Recipient");
        var crew = HandlerTestFixture.CreateCrew();
        var membership = HandlerTestFixture.CreateMembership(actor, crew);
        var gift = new Gift
        {
            Id = 10,
            CrewId = crew.Id,
            GiverUserId = giver.Id,
            GiverUser = giver,
            RecipientUserId = recipient.Id,
            RecipientUser = recipient,
            Type = GiftType.Direct,
            Amount = 20,
            CreatedAt = DateTime.UtcNow
        };

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(actor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        GiftLike? savedLike = null;
        var giftRepository = HandlerTestFixture.CreateGiftRepositoryMock();
        giftRepository
            .Setup(r => r.GetByIdWithUsersAsync(gift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gift);
        giftRepository
            .Setup(r => r.GetGiftLikeAsync(actor.Id, gift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GiftLike?)null);
        giftRepository
            .Setup(r => r.AddLikeAsync(It.IsAny<GiftLike>(), It.IsAny<CancellationToken>()))
            .Callback<GiftLike, CancellationToken>((like, _) => savedLike = like)
            .Returns(Task.CompletedTask);
        giftRepository
            .Setup(r => r.GetActiveLikeCountsForGiftsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [gift.Id] = 1 });

        var created = new List<Notification>();
        var notificationRepository = new Mock<INotificationRepository>(MockBehavior.Loose);
        notificationRepository
            .Setup(r => r.IsKindEnabledAsync(It.IsAny<int>(), It.IsAny<NotificationKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        notificationRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Notification>, CancellationToken>((items, _) => created.AddRange(items))
            .Returns(Task.CompletedTask);
        notificationRepository
            .Setup(r => r.GetUnreadCountAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var realtimeNotifier = new Mock<LiberationFleet.Server.Application.Common.Interfaces.INotificationRealtimeNotifier>(MockBehavior.Loose);
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();
        var notificationService = HandlerTestFixture.CreateNotificationService(
            notificationRepository,
            realtimeNotifier,
            unitOfWork);

        var handler = new ToggleGiftLikeCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(actor.Id).Object,
            membershipRepository.Object,
            giftRepository.Object,
            notificationService,
            unitOfWork.Object);

        var result = await handler.Handle(new ToggleGiftLikeCommand(gift.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Liked.Should().BeTrue();
        result.LikeCount.Should().Be(1);
        savedLike.Should().NotBeNull();
        savedLike!.AuthorNotified.Should().BeTrue();
        created.Should().HaveCount(2);
        created.Select(n => n.UserId).Should().BeEquivalentTo([giver.Id, recipient.Id]);
        created.Should().OnlyContain(n => n.Kind == NotificationKind.GiftEntryLiked);
        created.Should().OnlyContain(n => n.ActionUrl == $"/app/crew/gift-log/{gift.Id}?highlightId={gift.Id}");
    }

    [Fact]
    public async Task Handle_WhenUnlike_DoesNotCreateNotifications()
    {
        var user = HandlerTestFixture.CreateUser();
        var crew = HandlerTestFixture.CreateCrew();
        var membership = HandlerTestFixture.CreateMembership(user, crew);
        var gift = new Gift
        {
            Id = 11,
            CrewId = crew.Id,
            GiverUserId = 2,
            RecipientUserId = 3,
            GiverUser = HandlerTestFixture.CreateUser(id: 2),
            RecipientUser = HandlerTestFixture.CreateUser(id: 3),
            Type = GiftType.Direct,
            Amount = 5,
            CreatedAt = DateTime.UtcNow
        };

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var existing = new GiftLike
        {
            Id = 1,
            UserId = user.Id,
            GiftId = gift.Id,
            AuthorNotified = true,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var giftRepository = HandlerTestFixture.CreateGiftRepositoryMock();
        giftRepository
            .Setup(r => r.GetByIdWithUsersAsync(gift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gift);
        giftRepository
            .Setup(r => r.GetGiftLikeAsync(user.Id, gift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        giftRepository
            .Setup(r => r.GetActiveLikeCountsForGiftsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());

        var notificationRepository = new Mock<INotificationRepository>(MockBehavior.Loose);
        notificationRepository
            .Setup(r => r.IsKindEnabledAsync(It.IsAny<int>(), It.IsAny<NotificationKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new ToggleGiftLikeCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(user.Id).Object,
            membershipRepository.Object,
            giftRepository.Object,
            HandlerTestFixture.CreateNotificationService(notificationRepository: notificationRepository),
            HandlerTestFixture.CreateUnitOfWorkMock().Object);

        var result = await handler.Handle(new ToggleGiftLikeCommand(gift.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Liked.Should().BeFalse();
        existing.RemovedAt.Should().NotBeNull();
        notificationRepository.Verify(
            r => r.AddRangeAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
