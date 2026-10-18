using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Chats;

public class CrewChatsProposalServiceTests
{
    [Fact]
    public async Task TryApplyApproved_Create_DoesNotInsertASecondRoomOnRetry()
    {
        var change = new ProposalCrewChatChange
        {
            ProposalId = 9,
            Action = CrewChatProposalAction.Create,
            Purpose = "General",
            RoomType = ChatRoomType.Text,
            NameNonce = "nonce",
            NameCiphertext = "cipher",
            KeyVersion = 1
        };
        var proposal = new Proposal
        {
            Id = 9,
            CrewId = 3,
            AuthorUserId = 4,
            Kind = ProposalKind.CrewChatChange,
            Status = ProposalStatus.Approved
        };

        var proposalRepository = new Mock<IProposalRepository>(MockBehavior.Strict);
        proposalRepository
            .Setup(x => x.GetCrewChatChangeByProposalIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(change);

        var nextRoomId = 40;
        var chatRepository = new Mock<IChatRepository>(MockBehavior.Strict);
        chatRepository
            .Setup(x => x.AddRoomAsync(It.IsAny<ChatRoom>(), It.IsAny<CancellationToken>()))
            .Returns<ChatRoom, CancellationToken>((room, _) =>
            {
                room.Id = nextRoomId++;
                return Task.CompletedTask;
            });
        chatRepository
            .Setup(x => x.GetRoomByIdWithAuthorAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatRoom?)null);

        var cryptoRepository = new Mock<ICryptoRepository>(MockBehavior.Loose);
        var realtime = new Mock<IChatRealtimeNotifier>(MockBehavior.Loose);
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Loose);

        var service = new CrewChatsProposalService(
            proposalRepository.Object,
            chatRepository.Object,
            cryptoRepository.Object,
            realtime.Object,
            notificationService: null!,
            Mock.Of<IFleetRepository>(),
            Mock.Of<ICrewRepository>(),
            unitOfWork.Object);

        await service.TryApplyApprovedProposalAsync(proposal, CancellationToken.None);
        await service.TryApplyApprovedProposalAsync(proposal, CancellationToken.None);

        chatRepository.Verify(
            x => x.AddRoomAsync(It.IsAny<ChatRoom>(), It.IsAny<CancellationToken>()),
            Times.Once);
        change.IsApplied.Should().BeTrue();
        change.RoomId.Should().Be(40);
    }
}
