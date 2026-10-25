using FluentAssertions;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats.Commands.SendChatMessage;
using LiberationFleet.Server.Application.Features.Mentions;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Chats.Commands.SendChatMessage;

public class SendChatMessageCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenMessageIsAnonymous_OmitsActorIdentityFromNotifications()
    {
        var author = HandlerTestFixture.CreateUser(id: 1, username: "Author");
        var mentionedUser = HandlerTestFixture.CreateUser(id: 2, username: "Mentioned");
        var crew = HandlerTestFixture.CreateCrew(id: 7);
        var membership = HandlerTestFixture.CreateMembership(author, crew);
        var mentionedMembership = HandlerTestFixture.CreateMembership(mentionedUser, crew);
        var room = new ChatRoom
        {
            Id = 5,
            CrewId = crew.Id,
            RoomType = ChatRoomType.Text,
            Name = "General",
            LastActivityAt = DateTime.UtcNow
        };

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(author.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        membershipRepository
            .Setup(r => r.GetActiveMembersByCrewIdAsync(crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([membership, mentionedMembership]);

        ChatRoomMessage? savedMessage = null;
        var chatRepository = new Mock<IChatRepository>(MockBehavior.Loose);
        chatRepository
            .Setup(r => r.GetRoomByIdAsync(room.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);
        chatRepository
            .Setup(r => r.AddMessageAsync(It.IsAny<ChatRoomMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ChatRoomMessage, CancellationToken>((message, _) =>
            {
                message.Id = 77;
                message.AuthorUser = author;
                savedMessage = message;
            })
            .Returns(Task.CompletedTask);
        chatRepository
            .Setup(r => r.GetMessageByIdWithAuthorAsync(77, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => savedMessage);

        var cryptoRepository = new Mock<ICryptoRepository>(MockBehavior.Loose);
        cryptoRepository
            .Setup(r => r.UpsertEnvelopeAsync(It.IsAny<EncryptedContentEnvelope>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var realtimeNotifier = new Mock<IChatRealtimeNotifier>(MockBehavior.Loose);
        realtimeNotifier
            .Setup(r => r.NotifyMessageSentAsync(crew.Id, room.Id, It.IsAny<LiberationFleet.Server.Application.Features.Chats.Contracts.ChatMessageDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        realtimeNotifier
            .Setup(r => r.NotifyRoomActivityUpdatedAsync(crew.Id, room.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var createdNotifications = new List<Notification>();
        var notificationRepository = new Mock<INotificationRepository>(MockBehavior.Loose);
        notificationRepository
            .Setup(r => r.GetCrewMemberUserIdsAsync(crew.Id, author.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([mentionedUser.Id]);
        notificationRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Notification>, CancellationToken>((items, _) => createdNotifications.AddRange(items))
            .Returns(Task.CompletedTask);
        notificationRepository
            .Setup(r => r.GetUnreadCountAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var notificationService = HandlerTestFixture.CreateNotificationService(notificationRepository: notificationRepository);

        var mentionRepository = new Mock<IContentMentionRepository>(MockBehavior.Loose);
        mentionRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<ContentMention>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var contentMentionService = new ContentMentionService(
            membershipRepository.Object,
            HandlerTestFixture.CreateFleetRepositoryMock().Object,
            mentionRepository.Object,
            HandlerTestFixture.CreateUserRepositoryMock().Object,
            HandlerTestFixture.CreateUserBlockRepositoryMock().Object,
            notificationService,
            HandlerTestFixture.CreateUnitOfWorkMock().Object);

        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        userRepository
            .Setup(r => r.GetByIdWithProfileAsync(author.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        var handler = new SendChatMessageCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(author.Id).Object,
            membershipRepository.Object,
            userRepository.Object,
            HandlerTestFixture.CreateFleetRepositoryMock().Object,
            chatRepository.Object,
            cryptoRepository.Object,
            realtimeNotifier.Object,
            notificationService,
            contentMentionService,
            HandlerTestFixture.CreateUnitOfWorkMock().Object);

        var result = await handler.Handle(
            new SendChatMessageCommand(
                room.Id,
                "nonce",
                "ciphertext",
                1,
                "hello @Mentioned",
                [mentionedUser.Id],
                true),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        createdNotifications.Should().HaveCount(2);
        createdNotifications.Select(n => n.ActorUserId).Should().OnlyContain(actorUserId => actorUserId == null);
        createdNotifications.Should().ContainSingle(n =>
            n.Kind == NotificationKind.NewChatMessage
            && n.Body == "hello @Mentioned");
        createdNotifications.Should().ContainSingle(n =>
            n.Kind == NotificationKind.Mention
            && n.Title.Contains("Anonymous mentioned you"));
        notificationRepository.Verify(
            r => r.GetCrewMemberUserIdsAsync(crew.Id, author.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
