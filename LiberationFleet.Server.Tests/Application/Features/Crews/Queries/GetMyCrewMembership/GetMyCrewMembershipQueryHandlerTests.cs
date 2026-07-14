using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Crews.Queries.GetMyCrewMembership;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Crews.Queries.GetMyCrewMembership;

public class GetMyCrewMembershipQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenUserIsNotAuthenticated_ReturnsEmptyStatus()
    {
        var handler = CreateHandler(currentUserId: null);

        var result = await handler.Handle(new GetMyCrewMembershipQuery(), CancellationToken.None);

        result.HasCrew.Should().BeFalse();
        result.CrewId.Should().BeNull();
        result.CrewName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenUserHasNoCrew_ReturnsHasCrewFalse()
    {
        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.CrewMembership?)null);

        var handler = CreateHandler(membershipRepository: membershipRepository);

        var result = await handler.Handle(new GetMyCrewMembershipQuery(), CancellationToken.None);

        result.HasCrew.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenUserHasCrew_ReturnsMembershipDetails()
    {
        var user = HandlerTestFixture.CreateUser();
        var crew = HandlerTestFixture.CreateCrew(name: "Fleet Bravo", joinCode: "BRAVO123");
        var membership = HandlerTestFixture.CreateMembership(user, crew);

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var giftRepository = HandlerTestFixture.CreateGiftRepositoryMock();
        giftRepository
            .Setup(r => r.GetCrewmateGiftStatsAsync(user.Id, crew.Id, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CrewmateGiftStatsDto());

        var handler = CreateHandler(
            currentUserId: user.Id,
            membershipRepository: membershipRepository,
            giftRepository: giftRepository);

        var result = await handler.Handle(new GetMyCrewMembershipQuery(), CancellationToken.None);

        result.HasCrew.Should().BeTrue();
        result.CrewId.Should().Be(crew.Id);
        result.CrewName.Should().Be("Fleet Bravo");
        result.JoinCode.Should().Be("BRAVO123");
        result.CanCreateFleetProposals.Should().BeFalse();
        result.CanAttachFilesToFleetContent.Should().BeFalse();
    }

    private static GetMyCrewMembershipQueryHandler CreateHandler(
        int? currentUserId = 1,
        Mock<ICrewMembershipRepository>? membershipRepository = null,
        Mock<IGiftRepository>? giftRepository = null)
    {
        membershipRepository ??= HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        giftRepository ??= HandlerTestFixture.CreateGiftRepositoryMock();

        return new GetMyCrewMembershipQueryHandler(
            membershipRepository.Object,
            giftRepository.Object,
            HandlerTestFixture.CreateFleetRepositoryMock().Object,
            HandlerTestFixture.CreateContentTenureService(),
            HandlerTestFixture.CreateCurrentUserServiceMock(currentUserId).Object);
    }
}
