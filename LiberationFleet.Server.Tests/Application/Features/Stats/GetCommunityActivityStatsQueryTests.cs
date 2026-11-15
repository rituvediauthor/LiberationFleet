using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Stats.Queries.GetCommunityActivityStats;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Stats;

public class GetCommunityActivityStatsQueryTests
{
    [Fact]
    public async Task Handle_ReturnsRepositoryCountsWithThirtyDayWindow()
    {
        var membershipRepository = new Mock<ICrewMembershipRepository>(MockBehavior.Strict);
        membershipRepository
            .Setup(r => r.CountRecentlyActiveCommunityAsync(
                It.Is<DateTime>(d => d <= DateTime.UtcNow.AddDays(-29) && d >= DateTime.UtcNow.AddDays(-31)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((7, 42));

        var handler = new GetCommunityActivityStatsQueryHandler(membershipRepository.Object);
        var result = await handler.Handle(new GetCommunityActivityStatsQuery(), CancellationToken.None);

        result.ActiveCrewCount.Should().Be(7);
        result.ActiveCrewmateCount.Should().Be(42);
        result.ActiveWindowDays.Should().Be(30);
        membershipRepository.VerifyAll();
    }
}
