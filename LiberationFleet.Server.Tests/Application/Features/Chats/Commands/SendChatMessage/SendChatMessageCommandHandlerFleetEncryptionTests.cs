using FluentAssertions;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats.Commands.SendChatMessage;
using LiberationFleet.Server.Application.Features.Mentions;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Chats.Commands.SendChatMessage;

public class SendChatMessageCommandHandlerFleetEncryptionTests
{
    [Fact]
    public async Task Handle_FleetRoom_WithoutEncryptedPayload_Fails()
    {
        var (handler, _, cryptoRepository, _) = CreateFleetHandler();

        var result = await handler.Handle(
            new SendChatMessageCommand(
                RoomId: 9,
                Nonce: "",
                Ciphertext: "",
                KeyVersion: 1,
                Body: "plaintext fleet message",
                MentionedUserIds: [],
                IsAnonymous: false),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Encrypted message content is required.");
        cryptoRepository.Verify(
            r => r.UpsertEnvelopeAsync(It.IsAny<EncryptedContentEnvelope>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_FleetRoom_WithEncryptedPayload_StoresEnvelopeAndClearsBody()
    {
        var (handler, chatRepository, cryptoRepository, _) = CreateFleetHandler();
        EncryptedContentEnvelope? savedEnvelope = null;
        cryptoRepository
            .Setup(r => r.UpsertEnvelopeAsync(It.IsAny<EncryptedContentEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<EncryptedContentEnvelope, CancellationToken>((envelope, _) => savedEnvelope = envelope)
            .Returns(Task.CompletedTask);

        var result = await handler.Handle(
            new SendChatMessageCommand(
                RoomId: 9,
                Nonce: "fleet-nonce",
                Ciphertext: "fleet-ciphertext",
                KeyVersion: 3,
                Body: "should-not-persist",
                MentionedUserIds: [],
                IsAnonymous: false,
                NotificationPreview: "hello fleet"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        chatRepository.Verify(
            r => r.AddMessageAsync(
                It.Is<ChatRoomMessage>(m => m.Body == null && m.ChatRoomId == 9),
                It.IsAny<CancellationToken>()),
            Times.Once);
        savedEnvelope.Should().NotBeNull();
        savedEnvelope!.ContentType.Should().Be(EncryptedContentType.ChatRoomMessage);
        savedEnvelope.FleetId.Should().Be(44);
        savedEnvelope.CrewId.Should().BeNull();
        savedEnvelope.Nonce.Should().Be("fleet-nonce");
        savedEnvelope.Ciphertext.Should().Be("fleet-ciphertext");
        savedEnvelope.KeyVersion.Should().Be(3);
    }

    private static (
        SendChatMessageCommandHandler Handler,
        Mock<IChatRepository> ChatRepository,
        Mock<ICryptoRepository> CryptoRepository,
        Mock<IFleetRepository> FleetRepository) CreateFleetHandler()
    {
        var author = HandlerTestFixture.CreateUser(id: 1, username: "Author");
        var crew = HandlerTestFixture.CreateCrew(id: 7);
        var membership = HandlerTestFixture.CreateMembership(author, crew);
        var room = new ChatRoom
        {
            Id = 9,
            FleetId = 44,
            CrewId = null,
            RoomType = ChatRoomType.Text,
            Name = string.Empty,
            LastActivityAt = DateTime.UtcNow
        };

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(author.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        membershipRepository
            .Setup(r => r.GetActiveMembersByCrewIdAsync(crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([membership]);

        var chatRepository = new Mock<IChatRepository>(MockBehavior.Loose);
        chatRepository
            .Setup(r => r.GetRoomByIdAsync(room.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);
        chatRepository
            .Setup(r => r.AddMessageAsync(It.IsAny<ChatRoomMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ChatRoomMessage, CancellationToken>((message, _) =>
            {
                message.Id = 101;
                message.AuthorUser = author;
            })
            .Returns(Task.CompletedTask);
        // Skip realtime/notification fan-out; this suite asserts envelope persistence only.
        chatRepository
            .Setup(r => r.GetMessageByIdWithAuthorAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatRoomMessage?)null);

        var cryptoRepository = new Mock<ICryptoRepository>(MockBehavior.Loose);
        cryptoRepository
            .Setup(r => r.UpsertEnvelopeAsync(It.IsAny<EncryptedContentEnvelope>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var fleetRepository = HandlerTestFixture.CreateFleetRepositoryMock();
        fleetRepository
            .Setup(r => r.IsCrewInFleetAsync(crew.Id, room.FleetId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        fleetRepository
            .Setup(r => r.GetFleetCrewsAsync(room.FleetId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new FleetCrew { FleetId = room.FleetId!.Value, CrewId = crew.Id, Crew = crew }]);

        var realtimeNotifier = new Mock<IChatRealtimeNotifier>(MockBehavior.Loose);
        realtimeNotifier
            .Setup(r => r.NotifyMessageSentAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<LiberationFleet.Server.Application.Features.Chats.Contracts.ChatMessageDto>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        realtimeNotifier
            .Setup(r => r.NotifyRoomActivityUpdatedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var notificationRepository = new Mock<INotificationRepository>(MockBehavior.Loose);
        notificationRepository
            .Setup(r => r.IsKindEnabledAsync(It.IsAny<int>(), It.IsAny<NotificationKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        notificationRepository
            .Setup(r => r.GetCrewMemberUserIdsAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<int>());
        notificationRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()))
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
            fleetRepository.Object,
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
            fleetRepository.Object,
            chatRepository.Object,
            cryptoRepository.Object,
            realtimeNotifier.Object,
            notificationService,
            contentMentionService,
            HandlerTestFixture.CreateUnitOfWorkMock().Object);

        return (handler, chatRepository, cryptoRepository, fleetRepository);
    }
}
