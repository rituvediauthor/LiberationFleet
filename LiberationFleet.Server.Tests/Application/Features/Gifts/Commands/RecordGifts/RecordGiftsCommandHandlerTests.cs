using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGifts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts.Commands.RecordGifts;

public class RecordGiftsCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenRecordingDirectGift_AddsGiftWithoutApplyingReception()
    {
        var giver = HandlerTestFixture.CreateUser(id: 1);
        var recipient = HandlerTestFixture.CreateUser(id: 2);
        var crew = HandlerTestFixture.CreateCrew();
        var membership = HandlerTestFixture.CreateMembership(giver, crew);
        membership.IsInSeason = true;

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(giver.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        membershipRepository
            .Setup(r => r.IsUserInCrewAsync(recipient.Id, crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Gift? savedGift = null;
        var giftRepository = HandlerTestFixture.CreateGiftRepositoryMock();
        giftRepository
            .Setup(r => r.AddAsync(It.IsAny<Gift>(), It.IsAny<CancellationToken>()))
            .Callback<Gift, CancellationToken>((gift, _) => savedGift = gift)
            .Returns(Task.CompletedTask);
        giftRepository
            .Setup(r => r.GetByIdWithUsersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) =>
            {
                savedGift!.Id = id;
                savedGift.GiverUser = giver;
                savedGift.RecipientUser = recipient;
                savedGift.CrewPaymentPlatform = HandlerTestFixture.CreateCrewPaymentPlatform(1, crew.Id, "PayPal");
                return savedGift;
            });

        var handler = CreateHandler(
            currentUserId: giver.Id,
            membershipRepository: membershipRepository,
            giftRepository: giftRepository);

        var result = await handler.Handle(
            new RecordGiftsCommand(
            [
                new GiftRecordItem(30, 1, recipient.Id, null, false, "cycle")
            ]),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        savedGift.Should().NotBeNull();
        savedGift!.Type.Should().Be(GiftType.Direct);
        savedGift.CountsTowardReception.Should().BeTrue();
        savedGift.VerificationStatus.Should().Be(GiftVerificationStatus.Pending);
    }

    [Fact]
    public async Task Handle_WhenRecordingMiddlemanGift_DoesNotApplyReceptionUntilCompleted()
    {
        var giver = HandlerTestFixture.CreateUser(id: 1);
        var recipient = HandlerTestFixture.CreateUser(id: 2);
        var middleman = HandlerTestFixture.CreateUser(id: 3);
        var crew = HandlerTestFixture.CreateCrew();
        var membership = HandlerTestFixture.CreateMembership(giver, crew);
        membership.IsInSeason = true;

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(giver.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        membershipRepository
            .Setup(r => r.IsUserInCrewAsync(recipient.Id, crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        membershipRepository
            .Setup(r => r.IsUserInCrewAsync(middleman.Id, crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Gift? savedGift = null;
        var giftRepository = HandlerTestFixture.CreateGiftRepositoryMock();
        giftRepository
            .Setup(r => r.AddAsync(It.IsAny<Gift>(), It.IsAny<CancellationToken>()))
            .Callback<Gift, CancellationToken>((gift, _) => savedGift = gift)
            .Returns(Task.CompletedTask);
        giftRepository
            .Setup(r => r.GetByIdWithUsersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) =>
            {
                savedGift!.Id = id;
                savedGift.GiverUser = giver;
                savedGift.RecipientUser = recipient;
                savedGift.MiddlemanUser = middleman;
                savedGift.Type = GiftType.Initiated;
                savedGift.Amount = 40;
                savedGift.CrewPaymentPlatform = HandlerTestFixture.CreateCrewPaymentPlatform(1, crew.Id, "PayPal");
                return savedGift;
            });

        var handler = CreateHandler(
            currentUserId: giver.Id,
            membershipRepository: membershipRepository,
            giftRepository: giftRepository);

        var result = await handler.Handle(
            new RecordGiftsCommand(
            [
                new GiftRecordItem(40, 1, recipient.Id, middleman.Id, false, "cycle")
            ]),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        savedGift!.Type.Should().Be(GiftType.Initiated);
        savedGift.CountsTowardReception.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenGiverTriesToGiftSelf_ReturnsFailure()
    {
        var user = HandlerTestFixture.CreateUser();
        var crew = HandlerTestFixture.CreateCrew();
        var membership = HandlerTestFixture.CreateMembership(user, crew);
        membership.IsInSeason = true;

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var handler = CreateHandler(currentUserId: user.Id, membershipRepository: membershipRepository);

        var result = await handler.Handle(
            new RecordGiftsCommand(
            [
                new GiftRecordItem(20, 1, user.Id, null, false, "cycle")
            ]),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("You cannot give a gift to yourself.");
    }

    [Fact]
    public async Task Handle_WhenGiverNotInSeason_ReturnsFailure()
    {
        var user = HandlerTestFixture.CreateUser();
        var crew = HandlerTestFixture.CreateCrew();
        var membership = HandlerTestFixture.CreateMembership(user, crew);
        membership.IsInSeason = false;

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var handler = CreateHandler(currentUserId: user.Id, membershipRepository: membershipRepository);

        var result = await handler.Handle(
            new RecordGiftsCommand(
            [
                new GiftRecordItem(20, 1, 2, null, false, "cycle")
            ]),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("You must be in an active season to record gifts.");
    }

    [Fact]
    public async Task Handle_WhenMiddlemanIsGiver_ReturnsFailure()
    {
        var giver = HandlerTestFixture.CreateUser(id: 1);
        var recipient = HandlerTestFixture.CreateUser(id: 2);
        var crew = HandlerTestFixture.CreateCrew();
        var membership = HandlerTestFixture.CreateMembership(giver, crew);
        membership.IsInSeason = true;

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(giver.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var handler = CreateHandler(currentUserId: giver.Id, membershipRepository: membershipRepository);

        var result = await handler.Handle(
            new RecordGiftsCommand(
            [
                new GiftRecordItem(20, 1, recipient.Id, giver.Id, false, "cycle")
            ]),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid middleman selection.");
    }

    [Fact]
    public async Task Handle_WhenRecordingMultipleGifts_RecordsAllAndAppliesEachDirectGift()
    {
        var giver = HandlerTestFixture.CreateUser(id: 1);
        var recipientA = HandlerTestFixture.CreateUser(id: 2);
        var recipientB = HandlerTestFixture.CreateUser(id: 3);
        var crew = HandlerTestFixture.CreateCrew();
        var membership = HandlerTestFixture.CreateMembership(giver, crew);
        membership.IsInSeason = true;

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(giver.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        membershipRepository
            .Setup(r => r.IsUserInCrewAsync(It.IsAny<int>(), crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var giftRepository = HandlerTestFixture.CreateGiftRepositoryMock();
        giftRepository
            .Setup(r => r.AddAsync(It.IsAny<Gift>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        giftRepository
            .Setup(r => r.GetByIdWithUsersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new Gift
            {
                Id = id,
                GiverUser = giver,
                RecipientUser = id % 2 == 0 ? recipientA : recipientB,
                CrewPaymentPlatform = HandlerTestFixture.CreateCrewPaymentPlatform(1, crew.Id, "PayPal"),
                Amount = id == 1 ? 10 : 15,
                Type = GiftType.Direct,
                CreatedAt = DateTime.UtcNow
            });

        var handler = CreateHandler(
            currentUserId: giver.Id,
            membershipRepository: membershipRepository,
            giftRepository: giftRepository);

        var result = await handler.Handle(
            new RecordGiftsCommand(
            [
                new GiftRecordItem(10, 1, recipientA.Id, null, false, "cycle"),
                new GiftRecordItem(15, 1, recipientB.Id, null, false, "cycle")
            ]),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Be("2 gifts recorded.");
        giftRepository.Verify(r => r.AddAsync(It.IsAny<Gift>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static RecordGiftsCommandHandler CreateHandler(
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

        return new RecordGiftsCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(currentUserId).Object,
            membershipRepository.Object,
            giftRepository.Object,
            crewPaymentPlatformRepository.Object,
            unitOfWork.Object);
    }
}
