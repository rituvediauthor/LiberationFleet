using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Profile.Commands.UpdateProfile;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Profile.Commands.UpdateProfile;

public class UpdateProfileCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenUsernameTaken_ReturnsFailure()
    {
        var user = HandlerTestFixture.CreateUser();
        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();

        userRepository
            .Setup(r => r.GetByIdWithProfileAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        userRepository
            .Setup(r => r.IsUsernameTakenByOtherUserAsync("taken", user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler(currentUserId: user.Id, userRepository: userRepository);

        var result = await handler.Handle(new UpdateProfileCommand
        {
            Username = "taken",
            Email = user.Email
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Username is already taken");
    }

    [Fact]
    public async Task Handle_WhenValid_UpdatesUserAndPaymentPlatforms()
    {
        var user = HandlerTestFixture.CreateUser();
        user.PaymentPlatforms = new List<UserPaymentPlatform>
        {
            new()
            {
                Id = 1,
                PaymentPlatformId = 1,
                PaymentPlatform = new PaymentPlatform { Id = 1, Name = "PayPal" },
                Handle = "old"
            }
        };

        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();

        userRepository
            .Setup(r => r.GetByIdWithProfileAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        userRepository
            .Setup(r => r.IsUsernameTakenByOtherUserAsync(It.IsAny<string>(), user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        userRepository
            .Setup(r => r.IsEmailTakenByOtherUserAsync(It.IsAny<string>(), user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        userRepository
            .Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        User? updatedUser = null;
        userRepository
            .Setup(r => r.GetByIdWithProfileAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => updatedUser ?? user);

        var handler = CreateHandler(currentUserId: user.Id, userRepository: userRepository, unitOfWork: unitOfWork);

        var result = await handler.Handle(new UpdateProfileCommand
        {
            Username = "updateduser",
            Email = "updated@example.com",
            InNeedOfAid = false,
            EmergencyLevel = 1,
            NeedsSurvivalAid = true,
            PaymentPlatforms =
            [
                new PaymentPlatformAccountDto { PlatformId = 3, Handle = "@updated" }
            ]
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        user.Username.Should().Be("updateduser");
        user.PaymentPlatforms.Should().ContainSingle(p => p.PaymentPlatformId == 3 && p.Handle == "@updated");
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static UpdateProfileCommandHandler CreateHandler(
        int? currentUserId = 1,
        Mock<IUserRepository>? userRepository = null,
        Mock<IUnitOfWork>? unitOfWork = null,
        Mock<IGiftRepository>? giftRepository = null,
        Mock<ICrewMembershipRepository>? membershipRepository = null,
        Mock<IPaymentPlatformRepository>? paymentPlatformRepository = null)
    {
        userRepository ??= HandlerTestFixture.CreateUserRepositoryMock();
        unitOfWork ??= HandlerTestFixture.CreateUnitOfWorkMock();
        giftRepository ??= SetupDefaultGiftRepository(currentUserId);
        membershipRepository ??= SetupDefaultMembershipRepository(currentUserId);
        paymentPlatformRepository ??= HandlerTestFixture.CreatePaymentPlatformRepositoryMock();

        return new UpdateProfileCommandHandler(
            userRepository.Object,
            giftRepository.Object,
            membershipRepository.Object,
            paymentPlatformRepository.Object,
            HandlerTestFixture.CreateCurrentUserServiceMock(currentUserId).Object,
            HandlerTestFixture.CreateMutualAidServiceMock().Object,
            unitOfWork.Object);
    }

    private static Mock<IGiftRepository> SetupDefaultGiftRepository(int? userId)
    {
        var giftRepository = HandlerTestFixture.CreateGiftRepositoryMock();
        if (userId is not null)
        {
            giftRepository
                .Setup(r => r.GetUserGiftStatsAsync(userId.Value, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UserGiftStats());
        }

        return giftRepository;
    }

    private static Mock<ICrewMembershipRepository> SetupDefaultMembershipRepository(int? userId)
    {
        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        if (userId is not null)
        {
            membershipRepository
                .Setup(r => r.GetActiveMembershipAsync(userId.Value, It.IsAny<CancellationToken>()))
                .ReturnsAsync((CrewMembership?)null);
        }

        return membershipRepository;
    }
}
