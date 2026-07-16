using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Profile.Commands.UpdateProfile;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Application.Features.Profile.Queries.GetMyProfile;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Profile.Queries.GetMyProfile;

public class GetMyProfileQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenUserIsNotAuthenticated_ReturnsNull()
    {
        var handler = CreateHandler(currentUserId: null);

        var result = await handler.Handle(new GetMyProfileQuery(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenUserExists_ReturnsProfileFromDatabase()
    {
        var user = HandlerTestFixture.CreateUser();
        user.InNeedOfAid = true;
        user.EmergencyLevel = 2;
        user.PaymentPlatforms = new List<UserPaymentPlatform>
        {
            new()
            {
                Id = 1,
                CrewPaymentPlatformId = 1,
                CrewPaymentPlatform = HandlerTestFixture.CreateCrewPaymentPlatform(1, 1, "PayPal"),
                Handle = "james@example.com"
            }
        };

        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        userRepository
            .Setup(r => r.GetByIdWithProfileAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = CreateHandler(currentUserId: user.Id, userRepository: userRepository);

        var result = await handler.Handle(new GetMyProfileQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Username.Should().Be(user.Username);
        result.Email.Should().Be(user.Email);
        result.EmergencyLevel.Should().Be(2);
        result.PaymentPlatforms.Should().ContainSingle(p => p.PlatformId == 1 && p.Platform == "PayPal");
    }

    [Fact]
    public async Task Handle_WhenUserHasGifts_ReturnsStatsFromGiftRepository()
    {
        var user = HandlerTestFixture.CreateUser();
        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        userRepository
            .Setup(r => r.GetByIdWithProfileAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var giftStats = new CrewmateGiftStatsDto
        {
            LifetimeContributions = 150,
            SacrificeCountLastSeason = 3,
            AverageMonthlyContributions = 30,
            ReceptionThisYear = 40
        };

        var giftRepository = HandlerTestFixture.CreateGiftRepositoryMock();
        giftRepository
            .Setup(r => r.GetCrewmateGiftStatsAsync(user.Id, It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(giftStats);

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(HandlerTestFixture.CreateMembership(user, HandlerTestFixture.CreateCrew()));

        var handler = CreateHandler(
            currentUserId: user.Id,
            userRepository: userRepository,
            giftRepository: giftRepository,
            membershipRepository: membershipRepository);

        var result = await handler.Handle(new GetMyProfileQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Stats.LifetimeContributions.Should().Be(150);
        result.Stats.AverageMonthlyContributions.Should().Be(30);
        result.Stats.SacrificeCountLastSeason.Should().Be(3);
        result.Stats.ReceptionThisYear.Should().Be(40);
        result.Stats.MembershipStatus.Should().BeFalse();
    }

    private static GetMyProfileQueryHandler CreateHandler(
        int? currentUserId = 1,
        Mock<IUserRepository>? userRepository = null,
        Mock<IGiftRepository>? giftRepository = null,
        Mock<ICrewMembershipRepository>? membershipRepository = null,
        Mock<IMutualAidRepository>? mutualAidRepository = null)
    {
        userRepository ??= HandlerTestFixture.CreateUserRepositoryMock();
        giftRepository ??= SetupDefaultGiftRepository(currentUserId);
        membershipRepository ??= SetupDefaultMembershipRepository(currentUserId);
        mutualAidRepository ??= SetupDefaultMutualAidRepository();

        return new GetMyProfileQueryHandler(
            userRepository.Object,
            giftRepository.Object,
            membershipRepository.Object,
            mutualAidRepository.Object,
            HandlerTestFixture.CreateMutualAidServiceMock().Object,
            HandlerTestFixture.CreateCurrentUserServiceMock(currentUserId).Object,
            CreateDonationRepositoryMock().Object);
    }

    private static Mock<IAppDonationRepository> CreateDonationRepositoryMock()
    {
        var donationRepository = new Mock<IAppDonationRepository>(MockBehavior.Loose);
        donationRepository
            .Setup(r => r.SumCompletedUsdForUserInRangeAsync(
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        return donationRepository;
    }

    private static Mock<IMutualAidRepository> SetupDefaultMutualAidRepository()
    {
        var mutualAidRepository = new Mock<IMutualAidRepository>();
        mutualAidRepository
            .Setup(r => r.GetUnsatisfiedThresholdsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MonthlySurvivalThreshold>());
        return mutualAidRepository;
    }

    private static Mock<IGiftRepository> SetupDefaultGiftRepository(int? userId)
    {
        var giftRepository = HandlerTestFixture.CreateGiftRepositoryMock();
        if (userId is not null)
        {
            giftRepository
                .Setup(r => r.GetCrewmateGiftStatsAsync(userId.Value, It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CrewmateGiftStatsDto());
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
