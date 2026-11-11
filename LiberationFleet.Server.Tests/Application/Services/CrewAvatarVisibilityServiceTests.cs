using FluentAssertions;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Services;

public class CrewAvatarVisibilityServiceTests
{
    [Fact]
    public async Task GetUsersAllowed_ShortCircuitsOrganizersAndGrantedWithoutGiftQueries()
    {
        var crew = HandlerTestFixture.CreateCrew(id: 7);
        crew.AllowCrewmateFileAttachments = true;
        crew.MinimumContributionForAttachments = 100m;
        crew.MinimumCrewmateTenureDaysForAttachments = 30;

        var organizer = HandlerTestFixture.CreateMembership(
            HandlerTestFixture.CreateUser(1, "Org"),
            crew);
        organizer.IsOrganizer = true;

        var granted = HandlerTestFixture.CreateMembership(
            HandlerTestFixture.CreateUser(2, "Granted"),
            crew);
        granted.CanAttachFiles = true;

        var denied = HandlerTestFixture.CreateMembership(
            HandlerTestFixture.CreateUser(3, "Denied"),
            crew);
        denied.AttachmentsDenied = true;

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        var mutualAidRepository = new Mock<IMutualAidRepository>(MockBehavior.Strict);
        var tenureRepository = new Mock<IContentTenureRepository>(MockBehavior.Strict);
        var tenureService = new ContentTenureService(
            tenureRepository.Object,
            membershipRepository.Object,
            HandlerTestFixture.CreateFleetRepositoryMock().Object);

        var service = new CrewAvatarVisibilityService(
            membershipRepository.Object,
            mutualAidRepository.Object,
            tenureService);

        var allowed = await service.GetUsersAllowedToShowCrewAvatarAsync(
            crew.Id,
            [organizer, granted, denied],
            CancellationToken.None);

        allowed.Should().BeEquivalentTo(new[] { 1, 2 });
        mutualAidRepository.VerifyNoOtherCalls();
        tenureRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetUsersAllowed_BatchesLifetimeAndTenureForThresholdMembersOnly()
    {
        var crew = HandlerTestFixture.CreateCrew(id: 7);
        crew.AllowCrewmateFileAttachments = true;
        crew.MinimumContributionForAttachments = 50m;
        crew.MinimumCrewmateTenureDaysForAttachments = 10;

        var organizer = HandlerTestFixture.CreateMembership(
            HandlerTestFixture.CreateUser(1, "Org"),
            crew);
        organizer.IsOrganizer = true;

        var candidate = HandlerTestFixture.CreateMembership(
            HandlerTestFixture.CreateUser(4, "Candidate"),
            crew);
        var belowThreshold = HandlerTestFixture.CreateMembership(
            HandlerTestFixture.CreateUser(5, "Low"),
            crew);

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        var mutualAidRepository = new Mock<IMutualAidRepository>(MockBehavior.Strict);
        mutualAidRepository
            .Setup(r => r.GetLifetimeContributionsForUsersAsync(
                crew.Id,
                It.Is<IReadOnlyCollection<int>>(ids => ids.OrderBy(x => x).SequenceEqual(new[] { 4, 5 })),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [4] = 75m, [5] = 10m });

        var tenureRepository = new Mock<IContentTenureRepository>(MockBehavior.Strict);
        tenureRepository
            .Setup(r => r.GetCrewTenuresForUsersAsync(
                It.Is<IReadOnlyCollection<int>>(ids => ids.OrderBy(x => x).SequenceEqual(new[] { 4, 5 })),
                crew.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new UserCrewContentTenure
                {
                    UserId = 4,
                    CrewId = crew.Id,
                    AccruedTicks = TimeSpan.FromDays(20).Ticks,
                    ClockStartedAtUtc = null
                },
                new UserCrewContentTenure
                {
                    UserId = 5,
                    CrewId = crew.Id,
                    AccruedTicks = TimeSpan.FromDays(20).Ticks,
                    ClockStartedAtUtc = null
                }
            ]);

        var tenureService = new ContentTenureService(
            tenureRepository.Object,
            membershipRepository.Object,
            HandlerTestFixture.CreateFleetRepositoryMock().Object);

        var service = new CrewAvatarVisibilityService(
            membershipRepository.Object,
            mutualAidRepository.Object,
            tenureService);

        var allowed = await service.GetUsersAllowedToShowCrewAvatarAsync(
            crew.Id,
            [organizer, candidate, belowThreshold],
            CancellationToken.None);

        allowed.Should().BeEquivalentTo(new[] { 1, 4 });
        mutualAidRepository.VerifyAll();
        tenureRepository.VerifyAll();
    }
}
