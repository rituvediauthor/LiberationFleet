using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetCrewGiftLog;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts.Queries.GetCrewGiftLog;

public class GetCrewGiftLogQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenUserIsNotAuthenticated_ReturnsFailure()
    {
        var handler = CreateHandler(currentUserId: null);

        var result = await handler.Handle(new GetCrewGiftLogQuery(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized.");
    }

    [Fact]
    public async Task Handle_WhenUserHasNoCrew_ReturnsFailure()
    {
        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CrewMembership?)null);

        var handler = CreateHandler(membershipRepository: membershipRepository);

        var result = await handler.Handle(new GetCrewGiftLogQuery(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("You are not in a crew.");
    }

    [Fact]
    public async Task Handle_WhenUserHasCrew_ReturnsGiftLogFromDatabase()
    {
        var user = HandlerTestFixture.CreateUser();
        var crew = HandlerTestFixture.CreateCrew();
        var membership = HandlerTestFixture.CreateMembership(user, crew);
        var recipient = HandlerTestFixture.CreateUser(id: 2, username: "Ritu");

        var gift = new Gift
        {
            Id = 1,
            CrewId = crew.Id,
            GiverUserId = user.Id,
            GiverUser = user,
            RecipientUserId = recipient.Id,
            RecipientUser = recipient,
            Type = GiftType.Direct,
            Amount = 25,
            CrewPaymentPlatformId = 1,
            CrewPaymentPlatform = HandlerTestFixture.CreateCrewPaymentPlatform(1, crew.Id, "PayPal"),
            CreatedAt = DateTime.UtcNow
        };

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var giftRepository = HandlerTestFixture.CreateGiftRepositoryMock();
        giftRepository
            .Setup(r => r.GetLogPageByCrewIdAsync(
                crew.Id,
                It.IsAny<int>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GiftLogPage
            {
                Items = new List<Gift> { gift },
                HasMore = false
            });
        giftRepository
            .Setup(r => r.GetCompletedInitiatedGiftIdsAsync(crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<int>());
        giftRepository
            .Setup(r => r.GetCompletedGiftsByInitiatedIdsAsync(crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, Gift>());

        var cryptoRepository = new Mock<ICryptoRepository>();
        cryptoRepository
            .Setup(r => r.GetEnvelopesAsync(
                EncryptedContentType.GiftLogEntry,
                It.IsAny<IReadOnlyList<string>>(),
                crew.Id,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EncryptedContentEnvelope>());

        var handler = CreateHandler(
            currentUserId: user.Id,
            membershipRepository: membershipRepository,
            giftRepository: giftRepository,
            cryptoRepository: cryptoRepository);

        var result = await handler.Handle(new GetCrewGiftLogQuery(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().ContainSingle();
        result.Items[0].Message.Should().Contain("PayPal");
    }

    private static GetCrewGiftLogQueryHandler CreateHandler(
        int? currentUserId = 1,
        Mock<ICrewMembershipRepository>? membershipRepository = null,
        Mock<IGiftRepository>? giftRepository = null,
        Mock<ICryptoRepository>? cryptoRepository = null)
    {
        membershipRepository ??= HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        giftRepository ??= HandlerTestFixture.CreateGiftRepositoryMock();
        cryptoRepository ??= new Mock<ICryptoRepository>();
        cryptoRepository
            .Setup(r => r.GetEnvelopesAsync(
                It.IsAny<EncryptedContentType>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EncryptedContentEnvelope>());

        return new GetCrewGiftLogQueryHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(currentUserId).Object,
            membershipRepository.Object,
            giftRepository.Object,
            cryptoRepository.Object);
    }
}
