using FluentAssertions;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto.Queries.GetCrewKeyState;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Crypto.Queries.GetCrewKeyState;

public class GetCrewKeyStateQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenHistoricalVersionsExist_ReturnsAllMyWraps()
    {
        var membership = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membership
            .Setup(r => r.IsUserInCrewAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var historical = new[]
        {
            new CrewKeyDistribution
            {
                CrewId = 10,
                UserId = 1,
                KeyVersion = 2,
                WrappedCrewKey = "wrap-v2",
                WrapNonce = "nonce-v2",
                WrappedByUserId = 1
            },
            new CrewKeyDistribution
            {
                CrewId = 10,
                UserId = 1,
                KeyVersion = 1,
                WrappedCrewKey = "wrap-v1",
                WrapNonce = "nonce-v1",
                WrappedByUserId = 1
            }
        };

        var latestOnly = new[] { historical[0] };

        var cryptoRepository = new Mock<ICryptoRepository>(MockBehavior.Strict);
        cryptoRepository
            .Setup(r => r.GetLatestCrewKeyVersionAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        cryptoRepository
            .Setup(r => r.GetCrewKeyDistributionsAsync(10, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestOnly);
        cryptoRepository
            .Setup(r => r.GetCrewKeyDistributionsForUserAsync(10, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(historical);

        var handler = new GetCrewKeyStateQueryHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(1).Object,
            membership.Object,
            cryptoRepository.Object);

        var result = await handler.Handle(new GetCrewKeyStateQuery(10), CancellationToken.None);

        result.Should().NotBeNull();
        result!.LatestKeyVersion.Should().Be(2);
        result.MyDistribution.Should().NotBeNull();
        result.MyDistribution!.KeyVersion.Should().Be(2);
        result.MyHistoricalDistributions.Should().HaveCount(2);
        result.MyHistoricalDistributions.Select(d => d.KeyVersion).Should().BeEquivalentTo(new[] { 2, 1 });
        result.MyHistoricalDistributions.Should().Contain(d => d.KeyVersion == 1 && d.WrappedCrewKey == "wrap-v1");
        cryptoRepository.VerifyAll();
    }

    [Fact]
    public async Task Handle_WhenNoKeysExist_ReturnsEmptyState()
    {
        var membership = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membership
            .Setup(r => r.IsUserInCrewAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var cryptoRepository = new Mock<ICryptoRepository>(MockBehavior.Strict);
        cryptoRepository
            .Setup(r => r.GetLatestCrewKeyVersionAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var handler = new GetCrewKeyStateQueryHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(1).Object,
            membership.Object,
            cryptoRepository.Object);

        var result = await handler.Handle(new GetCrewKeyStateQuery(10), CancellationToken.None);

        result.Should().NotBeNull();
        result!.LatestKeyVersion.Should().BeNull();
        result.MyDistribution.Should().BeNull();
        result.MyHistoricalDistributions.Should().BeEmpty();
        cryptoRepository.VerifyAll();
    }
}
