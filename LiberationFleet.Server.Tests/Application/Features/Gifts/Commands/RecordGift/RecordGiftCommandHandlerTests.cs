using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGift;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts.Commands.RecordGift;

public class RecordGiftCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenRecordingDirectGift_AddsGiftToDatabase()
    {
        var giver = HandlerTestFixture.CreateUser(id: 1, username: "James");
        var recipient = HandlerTestFixture.CreateUser(id: 2, username: "Ritu");
        var crew = HandlerTestFixture.CreateCrew();
        var membership = HandlerTestFixture.CreateMembership(giver, crew);

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
                savedGift.PaymentPlatform = new PaymentPlatform { Id = 1, Name = "PayPal" };
                return savedGift;
            });

        var handler = CreateHandler(
            currentUserId: giver.Id,
            membershipRepository: membershipRepository,
            giftRepository: giftRepository);

        var result = await handler.Handle(
            new RecordGiftCommand(30, 1, recipient.Id, null, null),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        savedGift.Should().NotBeNull();
        savedGift!.Type.Should().Be(GiftType.Direct);
        savedGift.Amount.Should().Be(30);
    }

    [Fact]
    public async Task Handle_WhenGiverTriesToGiftSelf_ReturnsFailure()
    {
        var user = HandlerTestFixture.CreateUser();
        var crew = HandlerTestFixture.CreateCrew();
        var membership = HandlerTestFixture.CreateMembership(user, crew);

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var handler = CreateHandler(currentUserId: user.Id, membershipRepository: membershipRepository);

        var result = await handler.Handle(
            new RecordGiftCommand(20, 3, user.Id, null, null),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("You cannot give a gift to yourself.");
    }

    private static RecordGiftCommandHandler CreateHandler(
        int? currentUserId = 1,
        Mock<ICrewMembershipRepository>? membershipRepository = null,
        Mock<IGiftRepository>? giftRepository = null,
        Mock<IPaymentPlatformRepository>? paymentPlatformRepository = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        membershipRepository ??= HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        giftRepository ??= HandlerTestFixture.CreateGiftRepositoryMock();
        paymentPlatformRepository ??= HandlerTestFixture.CreatePaymentPlatformRepositoryMock();
        unitOfWork ??= HandlerTestFixture.CreateUnitOfWorkMock();

        return new RecordGiftCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(currentUserId).Object,
            membershipRepository.Object,
            giftRepository.Object,
            paymentPlatformRepository.Object,
            unitOfWork.Object);
    }
}
