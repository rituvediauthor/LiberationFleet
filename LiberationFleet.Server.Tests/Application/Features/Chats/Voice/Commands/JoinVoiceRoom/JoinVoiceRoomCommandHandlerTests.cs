using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats.Voice.Commands.JoinVoiceRoom;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Chats.Voice.Commands.JoinVoiceRoom;

public class JoinVoiceRoomCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenRoomIsText_ReturnsFailure()
    {
        var handler = CreateHandler(
            room: new ChatRoom { Id = 1, CrewId = 10, RoomType = ChatRoomType.Text, IsDeleted = false },
            userId: 5);

        var result = await handler.Handle(new JoinVoiceRoomCommand(1), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("This room is not a voice channel.");
    }

    [Fact]
    public async Task Handle_WhenUserNotInCrew_ReturnsFailure()
    {
        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.IsUserInCrewAsync(5, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler(
            room: new ChatRoom { Id = 1, CrewId = 10, RoomType = ChatRoomType.Voice, IsDeleted = false },
            userId: 5,
            membershipRepository: membershipRepository);

        var result = await handler.Handle(new JoinVoiceRoomCommand(1), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("You are not in this crew.");
    }

    [Fact]
    public async Task Handle_WhenAdultContentBlocked_ReturnsNotFound()
    {
        var user = HandlerTestFixture.CreateUser();
        user.AdultContentPreference = AdultContentPreference.Block;

        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        userRepository
            .Setup(r => r.GetByIdWithProfileAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = CreateHandler(
            room: new ChatRoom { Id = 1, CrewId = 10, RoomType = ChatRoomType.Voice, IsAdultContent = true, IsDeleted = false },
            userId: 5,
            userRepository: userRepository);

        var result = await handler.Handle(new JoinVoiceRoomCommand(1), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Chat room not found.");
    }

    [Fact]
    public async Task Handle_WhenValidVoiceRoom_ReturnsTokenAndRemovesExistingSession()
    {
        var existingSession = new VoiceParticipantSession
        {
            Id = 99,
            UserId = 5,
            CrewId = 10,
            ChatRoomId = 2
        };

        var voicePresenceRepository = new Mock<IVoicePresenceRepository>(MockBehavior.Strict);
        voicePresenceRepository
            .Setup(r => r.GetActiveByUserAndCrewAsync(5, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSession);
        voicePresenceRepository
            .Setup(r => r.RemoveAsync(existingSession, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        voicePresenceRepository
            .Setup(r => r.AddAsync(It.IsAny<VoiceParticipantSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var liveKitAdminService = new Mock<ILiveKitAdminService>(MockBehavior.Strict);
        liveKitAdminService
            .Setup(s => s.RemoveParticipantAsync("voice-crew-10-room-2", "5", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var liveKitTokenService = new Mock<ILiveKitTokenService>(MockBehavior.Strict);
        liveKitTokenService
            .Setup(s => s.CreateRoomToken("5", It.IsAny<string>(), "voice-crew-10-room-1"))
            .Returns("livekit-token");
        liveKitTokenService
            .Setup(s => s.GetWebSocketUrl())
            .Returns("ws://localhost:7880");

        var handler = CreateHandler(
            room: new ChatRoom { Id = 1, CrewId = 10, RoomType = ChatRoomType.Voice, IsDeleted = false },
            userId: 5,
            voicePresenceRepository: voicePresenceRepository,
            liveKitAdminService: liveKitAdminService,
            liveKitTokenService: liveKitTokenService);

        var result = await handler.Handle(new JoinVoiceRoomCommand(1), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Token.Should().Be("livekit-token");
        result.WsUrl.Should().Be("ws://localhost:7880");
        result.PreviousChatRoomId.Should().Be(2);
        liveKitAdminService.Verify(
            s => s.RemoveParticipantAsync("voice-crew-10-room-2", "5", It.IsAny<CancellationToken>()),
            Times.Once);
        voicePresenceRepository.Verify(r => r.RemoveAsync(existingSession, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static JoinVoiceRoomCommandHandler CreateHandler(
        ChatRoom room,
        int userId,
        Mock<ICrewMembershipRepository>? membershipRepository = null,
        Mock<IUserRepository>? userRepository = null,
        Mock<IChatRepository>? chatRepository = null,
        Mock<IVoicePresenceRepository>? voicePresenceRepository = null,
        Mock<ILiveKitAdminService>? liveKitAdminService = null,
        Mock<ILiveKitTokenService>? liveKitTokenService = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(u => u.UserId).Returns(userId);

        membershipRepository ??= HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.IsUserInCrewAsync(userId, room.CrewId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        userRepository ??= HandlerTestFixture.CreateUserRepositoryMock();
        var user = HandlerTestFixture.CreateUser();
        user.Id = userId;
        userRepository
            .Setup(r => r.GetByIdWithProfileAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        chatRepository ??= new Mock<IChatRepository>(MockBehavior.Strict);
        chatRepository
            .Setup(r => r.GetRoomByIdAsync(room.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);

        voicePresenceRepository ??= new Mock<IVoicePresenceRepository>(MockBehavior.Strict);
        voicePresenceRepository
            .Setup(r => r.GetActiveByUserAndCrewAsync(userId, room.CrewId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VoiceParticipantSession?)null);
        voicePresenceRepository
            .Setup(r => r.AddAsync(It.IsAny<VoiceParticipantSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        liveKitAdminService ??= new Mock<ILiveKitAdminService>(MockBehavior.Strict);
        liveKitTokenService ??= new Mock<ILiveKitTokenService>(MockBehavior.Strict);
        liveKitTokenService
            .Setup(s => s.CreateRoomToken(userId.ToString(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("livekit-token");
        liveKitTokenService
            .Setup(s => s.GetWebSocketUrl())
            .Returns("ws://localhost:7880");

        return new JoinVoiceRoomCommandHandler(
            currentUser.Object,
            membershipRepository.Object,
            userRepository.Object,
            chatRepository.Object,
            voicePresenceRepository.Object,
            liveKitTokenService.Object,
            liveKitAdminService.Object,
            HandlerTestFixture.CreateUnitOfWorkMock().Object);
    }
}
