using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetCrewGiftLog;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
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
            .Setup(r => r.GetCompletedGiftsByInitiatedIdsAsync(crew.Id, It.IsAny<IEnumerable<int>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, Gift>());
        giftRepository
            .Setup(r => r.GetActiveLikeCountsForGiftsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());
        giftRepository
            .Setup(r => r.GetActiveLikedGiftIdsByUserAsync(user.Id, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        giftRepository
            .Setup(r => r.GetCommentCountsForGiftsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());
        giftRepository
            .Setup(r => r.GetSeasonStartDatesForGiftsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, DateTime?>());
        giftRepository
            .Setup(r => r.EnsureGiftLogSchemaAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cryptoRepository = new Mock<ICryptoRepository>();
        cryptoRepository
            .Setup(r => r.GetEnvelopesAsync(
                EncryptedContentType.GiftLogEntry,
                It.IsAny<IReadOnlyList<string>>(),
                crew.Id,
                null,
                It.IsAny<int?>(),
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

    [Fact]
    public async Task Handle_WhenGiftLogPageFailsAfterRepair_ReturnsSuccessWithEmptyItems()
    {
        var user = HandlerTestFixture.CreateUser();
        var crew = HandlerTestFixture.CreateCrew();
        var membership = HandlerTestFixture.CreateMembership(user, crew);

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
            .ThrowsAsync(new InvalidOperationException("Invalid column name 'LibraryItemTitle'."));
        giftRepository
            .Setup(r => r.EnsureGiftLogSchemaAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(
            currentUserId: user.Id,
            membershipRepository: membershipRepository,
            giftRepository: giftRepository);

        var result = await handler.Handle(new GetCrewGiftLogQuery(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().BeEmpty();
        result.HasMore.Should().BeFalse();
        giftRepository.Verify(r => r.EnsureGiftLogSchemaAsync(It.IsAny<CancellationToken>()), Times.Once);
        giftRepository.Verify(
            r => r.GetLogPageByCrewIdAsync(
                crew.Id,
                It.IsAny<int>(),
                null,
                null,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WhenGiftLogPageFailsThenRepairSucceeds_ReturnsItems()
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
            .SetupSequence(r => r.GetLogPageByCrewIdAsync(
                crew.Id,
                It.IsAny<int>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Invalid column name 'LibraryItemTitle'."))
            .ReturnsAsync(new GiftLogPage
            {
                Items = new List<Gift> { gift },
                HasMore = false
            });
        giftRepository
            .Setup(r => r.EnsureGiftLogSchemaAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        giftRepository
            .Setup(r => r.GetCompletedGiftsByInitiatedIdsAsync(crew.Id, It.IsAny<IEnumerable<int>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, Gift>());
        giftRepository
            .Setup(r => r.GetActiveLikeCountsForGiftsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());
        giftRepository
            .Setup(r => r.GetActiveLikedGiftIdsByUserAsync(user.Id, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        giftRepository
            .Setup(r => r.GetCommentCountsForGiftsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());
        giftRepository
            .Setup(r => r.GetSeasonStartDatesForGiftsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, DateTime?>());

        var handler = CreateHandler(
            currentUserId: user.Id,
            membershipRepository: membershipRepository,
            giftRepository: giftRepository);

        var result = await handler.Handle(new GetCrewGiftLogQuery(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().ContainSingle();
        giftRepository.Verify(r => r.EnsureGiftLogSchemaAsync(It.IsAny<CancellationToken>()), Times.Once);
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
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EncryptedContentEnvelope>());

        return new GetCrewGiftLogQueryHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(currentUserId).Object,
            membershipRepository.Object,
            giftRepository.Object,
            cryptoRepository.Object,
            new Mock<ILogger<GetCrewGiftLogQueryHandler>>().Object);
    }
}
