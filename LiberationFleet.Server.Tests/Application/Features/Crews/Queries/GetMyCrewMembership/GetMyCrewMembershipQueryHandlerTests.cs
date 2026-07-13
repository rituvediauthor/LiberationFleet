using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
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

        var handler = CreateHandler(currentUserId: user.Id, membershipRepository: membershipRepository);

        var result = await handler.Handle(new GetMyCrewMembershipQuery(), CancellationToken.None);

        result.HasCrew.Should().BeTrue();
        result.CrewId.Should().Be(crew.Id);
        result.CrewName.Should().Be("Fleet Bravo");
        result.JoinCode.Should().Be("BRAVO123");
    }

    private static GetMyCrewMembershipQueryHandler CreateHandler(
        int? currentUserId = 1,
        Mock<ICrewMembershipRepository>? membershipRepository = null)
    {
        membershipRepository ??= HandlerTestFixture.CreateCrewMembershipRepositoryMock();

        return new GetMyCrewMembershipQueryHandler(
            membershipRepository.Object,
            HandlerTestFixture.CreateGiftRepositoryMock().Object,
            HandlerTestFixture.CreateCurrentUserServiceMock(currentUserId).Object);
    }
}
