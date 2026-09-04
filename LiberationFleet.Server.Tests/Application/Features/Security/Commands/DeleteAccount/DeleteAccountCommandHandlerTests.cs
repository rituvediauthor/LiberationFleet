using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Fleets;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Security.Commands.DeleteAccount;
using LiberationFleet.Server.Application.Features.Security.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Security.Commands.DeleteAccount;

public class DeleteAccountCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenPasswordIncorrect_ReturnsError()
    {
        var user = new User
        {
            Id = 9,
            Username = "alice",
            Email = "alice@example.com",
            PasswordHash = "hash",
            IsActive = true
        };

        var users = HandlerTestFixture.CreateUserRepositoryMock();
        users.Setup(r => r.GetByIdWithProfileAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var passwordHasher = HandlerTestFixture.CreatePasswordHasherMock(verifyResult: false);
        var handler = CreateHandler(users.Object, passwordHasher.Object, userId: 9);

        var result = await handler.Handle(
            new DeleteAccountCommand(new DeleteAccountRequest { CurrentPassword = "wrong" }),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Current password is incorrect.");
        user.IsActive.Should().BeTrue();
        user.Username.Should().Be("alice");
    }

    [Fact]
    public async Task Handle_WhenPasswordValid_AnonymizesAndDeactivates()
    {
        var user = new User
        {
            Id = 9,
            Username = "alice",
            Email = "alice@example.com",
            PasswordHash = "hash",
            IsActive = true,
            AvatarResourceId = "avatar-1",
            NeedsSurvivalAid = true,
            InNeedOfAid = true
        };
        user.PaymentPlatforms.Add(new UserPaymentPlatform
        {
            Id = 1,
            UserId = 9,
            CrewPaymentPlatformId = 3,
            Handle = "alicepay"
        });

        var users = HandlerTestFixture.CreateUserRepositoryMock();
        users.Setup(r => r.GetByIdWithProfileAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        users.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var memberships = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        memberships
            .Setup(r => r.GetActiveMembershipAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CrewMembership?)null);

        var fleets = HandlerTestFixture.CreateFleetRepositoryMock();
        fleets
            .Setup(r => r.GetFleetMembershipForUserAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FleetMembership?)null);

        var passwordHasher = HandlerTestFixture.CreatePasswordHasherMock("scrambled", verifyResult: true);
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();
        var handler = CreateHandler(
            users.Object,
            passwordHasher.Object,
            memberships.Object,
            fleets.Object,
            unitOfWork.Object,
            userId: 9);

        var result = await handler.Handle(
            new DeleteAccountCommand(new DeleteAccountRequest { CurrentPassword = "correct" }),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        user.Username.Should().Be("deleted9");
        user.Email.Should().Be("deleted-9@deleted.invalid");
        user.PasswordHash.Should().Be("scrambled");
        user.AvatarResourceId.Should().BeNull();
        user.NeedsSurvivalAid.Should().BeFalse();
        user.PaymentPlatforms.Should().BeEmpty();
        users.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    private static DeleteAccountCommandHandler CreateHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ICrewMembershipRepository? memberships = null,
        IFleetRepository? fleets = null,
        IUnitOfWork? unitOfWork = null,
        int? userId = 9)
    {
        memberships ??= HandlerTestFixture.CreateCrewMembershipRepositoryMock().Object;
        fleets ??= HandlerTestFixture.CreateFleetRepositoryMock().Object;
        unitOfWork ??= HandlerTestFixture.CreateUnitOfWorkMock().Object;

        var libraryCleanup = new LibraryMemberCleanupService(
            Mock.Of<ILibraryRepository>(),
            Mock.Of<ICryptoRepository>(),
            new LibraryRequestCleanupHelper(
                Mock.Of<ILibraryRepository>(),
                Mock.Of<ICryptoRepository>()));
        var emptyCrewCleanup = new EmptyCrewCleanupService(
            memberships,
            Mock.Of<ICrewCleanupRepository>());
        var fleetMembership = new FleetMembershipService(
            fleets,
            HandlerTestFixture.CreateContentTenureService());
        var tenure = HandlerTestFixture.CreateContentTenureService();

        return new DeleteAccountCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(userId).Object,
            users,
            passwordHasher,
            memberships,
            fleets,
            HandlerTestFixture.CreateMutualAidServiceMock().Object,
            libraryCleanup,
            emptyCrewCleanup,
            fleetMembership,
            tenure,
            unitOfWork);
    }
}
