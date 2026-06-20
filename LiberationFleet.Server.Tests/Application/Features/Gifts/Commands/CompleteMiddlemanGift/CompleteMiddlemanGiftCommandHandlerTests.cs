using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Commands.CompleteMiddlemanGift;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts.Commands.CompleteMiddlemanGift;

public class CompleteMiddlemanGiftCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenPlatformMissing_ReturnsFailure()
    {
        var handler = CreateHandler(currentUserId: 3);

        var result = await handler.Handle(
            new CompleteMiddlemanGiftCommand(1, 0),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("A payment platform is required to complete this gift.");
    }

    [Fact]
    public async Task Handle_WhenNotMiddleman_ReturnsFailure()
    {
        var middleman = HandlerTestFixture.CreateUser(id: 3);
        var recipient = CreateUserWithPlatform(2, 10, "Venmo");
        var initiated = CreateInitiatedGift(middleman, recipient, middlemanUserId: 99);

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(middleman.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(HandlerTestFixture.CreateMembership(middleman, HandlerTestFixture.CreateCrew()));

        var giftRepository = HandlerTestFixture.CreateGiftRepositoryMock();
        giftRepository
            .Setup(r => r.GetByIdWithUsersAsync(initiated.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(initiated);

        var handler = CreateHandler(
            currentUserId: middleman.Id,
            membershipRepository: membershipRepository,
            giftRepository: giftRepository);

        var result = await handler.Handle(
            new CompleteMiddlemanGiftCommand(initiated.Id, 10),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("You are not the middleman for this gift.");
    }

    [Fact]
    public async Task Handle_WhenPlatformNotSharedWithRecipient_ReturnsFailure()
    {
        var middleman = CreateUserWithPlatform(3, 10, "PayPal");
        var recipient = CreateUserWithPlatform(2, 20, "Venmo");
        var crew = HandlerTestFixture.CreateCrew();
        var initiated = CreateInitiatedGift(middleman, recipient, middlemanUserId: middleman.Id, middlemanUser: middleman);

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(middleman.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(HandlerTestFixture.CreateMembership(middleman, crew));

        var giftRepository = HandlerTestFixture.CreateGiftRepositoryMock();
        giftRepository
            .Setup(r => r.GetByIdWithUsersAsync(initiated.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(initiated);
        giftRepository
            .Setup(r => r.HasCompletedInitiatedGiftAsync(initiated.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler(
            currentUserId: middleman.Id,
            membershipRepository: membershipRepository,
            giftRepository: giftRepository);

        var result = await handler.Handle(
            new CompleteMiddlemanGiftCommand(initiated.Id, 10),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Selected payment platform is not shared with the recipient.");
    }

    [Fact]
    public async Task Handle_WhenValid_CompletesGiftWithoutApplyingReception()
    {
        var middleman = CreateUserWithPlatform(3, 10, "Cash App");
        var recipient = CreateUserWithPlatform(2, 10, "Cash App");
        var giver = HandlerTestFixture.CreateUser(id: 1);
        var crew = HandlerTestFixture.CreateCrew();
        var initiated = CreateInitiatedGift(giver, recipient, middlemanUserId: middleman.Id, middlemanUser: middleman);
        initiated.CrewId = crew.Id;
        initiated.VerificationStatus = GiftVerificationStatus.MiddlemanReceivedFunds;

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(middleman.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(HandlerTestFixture.CreateMembership(middleman, crew));

        Gift? savedGift = null;
        var giftRepository = HandlerTestFixture.CreateGiftRepositoryMock();
        giftRepository
            .Setup(r => r.GetByIdWithUsersAsync(initiated.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(initiated);
        giftRepository
            .Setup(r => r.HasCompletedInitiatedGiftAsync(initiated.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        giftRepository
            .Setup(r => r.AddAsync(It.IsAny<Gift>(), It.IsAny<CancellationToken>()))
            .Callback<Gift, CancellationToken>((gift, _) => savedGift = gift)
            .Returns(Task.CompletedTask);
        giftRepository
            .Setup(r => r.GetByIdWithUsersAsync(It.Is<int>(id => id != initiated.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) =>
            {
                savedGift!.Id = id;
                savedGift.GiverUser = giver;
                savedGift.RecipientUser = recipient;
                savedGift.MiddlemanUser = middleman;
                savedGift.CrewPaymentPlatform = HandlerTestFixture.CreateCrewPaymentPlatform(10, crew.Id, "Cash App");
                return savedGift;
            });

        var handler = CreateHandler(
            currentUserId: middleman.Id,
            membershipRepository: membershipRepository,
            giftRepository: giftRepository);

        var result = await handler.Handle(
            new CompleteMiddlemanGiftCommand(initiated.Id, 10),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        savedGift.Should().NotBeNull();
        savedGift!.Type.Should().Be(GiftType.Completed);
        savedGift.CountsTowardReception.Should().BeTrue();
        savedGift.InitiatedGiftId.Should().Be(initiated.Id);
        savedGift.VerificationStatus.Should().Be(GiftVerificationStatus.AwaitingRecipientVerification);
    }

    private static CompleteMiddlemanGiftCommandHandler CreateHandler(
        int? currentUserId = 1,
        Mock<ICrewMembershipRepository>? membershipRepository = null,
        Mock<IGiftRepository>? giftRepository = null,
        Mock<ICrewPaymentPlatformRepository>? crewPaymentPlatformRepository = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        membershipRepository ??= HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        giftRepository ??= HandlerTestFixture.CreateGiftRepositoryMock();
        crewPaymentPlatformRepository ??= HandlerTestFixture.CreateCrewPaymentPlatformRepositoryMock();
        unitOfWork ??= HandlerTestFixture.CreateUnitOfWorkMock();

        if (membershipRepository.Setups.Count == 0)
        {
            membershipRepository
                .Setup(r => r.GetActiveMembershipAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(HandlerTestFixture.CreateMembership(HandlerTestFixture.CreateUser(), HandlerTestFixture.CreateCrew()));
        }

        return new CompleteMiddlemanGiftCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(currentUserId).Object,
            membershipRepository.Object,
            giftRepository.Object,
            crewPaymentPlatformRepository.Object,
            unitOfWork.Object);
    }

    private static User CreateUserWithPlatform(int userId, int platformId, string platformName)
    {
        var platform = HandlerTestFixture.CreateCrewPaymentPlatform(platformId, 1, platformName);
        return new User
        {
            Id = userId,
            Username = $"user{userId}",
            PaymentPlatforms =
            [
                new UserPaymentPlatform
                {
                    UserId = userId,
                    CrewPaymentPlatformId = platformId,
                    CrewPaymentPlatform = platform,
                    Handle = $"@{userId}"
                }
            ]
        };
    }

    private static Gift CreateInitiatedGift(
        User giver,
        User recipient,
        int middlemanUserId,
        User? middlemanUser = null)
    {
        return new Gift
        {
            Id = 42,
            CrewId = 1,
            GiverUserId = giver.Id,
            RecipientUserId = recipient.Id,
            MiddlemanUserId = middlemanUserId,
            MiddlemanUser = middlemanUser,
            RecipientUser = recipient,
            Type = GiftType.Initiated,
            Amount = 25m
        };
    }
}
