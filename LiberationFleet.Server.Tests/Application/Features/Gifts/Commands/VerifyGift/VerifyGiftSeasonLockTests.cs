using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Commands.VerifyGift;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts.Commands.VerifyGift;

public class VerifyGiftSeasonLockTests
{
    [Fact]
    public async Task Handle_WhenPastSeasonAndNotAccountant_ReturnsLockedFailure()
    {
        var user = HandlerTestFixture.CreateUser();
        var crew = HandlerTestFixture.CreateCrew();
        crew.CurrentSeasonStartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var membership = HandlerTestFixture.CreateMembership(user, crew);
        membership.IsInSeason = true;
        membership.IsAccountant = false;

        var gift = new Gift
        {
            Id = 5,
            CrewId = crew.Id,
            GiverUserId = 2,
            RecipientUserId = user.Id,
            GiverUser = HandlerTestFixture.CreateUser(id: 2),
            RecipientUser = user,
            Type = GiftType.Direct,
            Amount = 10,
            VerificationStatus = GiftVerificationStatus.Pending,
            CreatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonCycle = new SeasonCycle
            {
                Id = 99,
                CrewId = crew.Id,
                SeasonStartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var giftRepository = HandlerTestFixture.CreateGiftRepositoryMock();
        giftRepository
            .Setup(r => r.GetByIdWithUsersAsync(gift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gift);

        var handler = new VerifyGiftCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(user.Id).Object,
            membershipRepository.Object,
            giftRepository.Object,
            HandlerTestFixture.CreateCrewPaymentPlatformRepositoryMock().Object,
            HandlerTestFixture.CreateMutualAidServiceMock().Object,
            HandlerTestFixture.CreateUnitOfWorkMock().Object);

        var result = await handler.Handle(
            new VerifyGiftCommand(gift.Id, GiftVerificationAction.ConfirmReceived),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("locked");
    }

    [Fact]
    public async Task Handle_WhenPastSeasonAndAccountant_AllowsAvailableAction()
    {
        var user = HandlerTestFixture.CreateUser();
        var crew = HandlerTestFixture.CreateCrew();
        crew.CurrentSeasonStartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var membership = HandlerTestFixture.CreateMembership(user, crew);
        membership.IsInSeason = true;
        membership.IsAccountant = true;

        var gift = new Gift
        {
            Id = 6,
            CrewId = crew.Id,
            GiverUserId = 2,
            RecipientUserId = user.Id,
            GiverUser = HandlerTestFixture.CreateUser(id: 2),
            RecipientUser = user,
            Type = GiftType.Direct,
            Amount = 10,
            VerificationStatus = GiftVerificationStatus.Pending,
            CreatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonCycle = new SeasonCycle
            {
                Id = 100,
                CrewId = crew.Id,
                SeasonStartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var giftRepository = HandlerTestFixture.CreateGiftRepositoryMock();
        giftRepository
            .Setup(r => r.GetByIdWithUsersAsync(gift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gift);
        giftRepository
            .Setup(r => r.GetCompletedGiftForInitiatedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Gift?)null);

        var mutualAid = HandlerTestFixture.CreateMutualAidServiceMock();
        mutualAid
            .Setup(s => s.ApplyGiftReceptionAsync(It.IsAny<Gift>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mutualAid
            .Setup(s => s.OnCrewContributionsChangedAsync(crew.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();
        var handler = new VerifyGiftCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(user.Id).Object,
            membershipRepository.Object,
            giftRepository.Object,
            HandlerTestFixture.CreateCrewPaymentPlatformRepositoryMock().Object,
            mutualAid.Object,
            unitOfWork.Object);

        var result = await handler.Handle(
            new VerifyGiftCommand(gift.Id, GiftVerificationAction.ConfirmReceived),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        gift.VerificationStatus.Should().Be(GiftVerificationStatus.Verified);
    }
}
