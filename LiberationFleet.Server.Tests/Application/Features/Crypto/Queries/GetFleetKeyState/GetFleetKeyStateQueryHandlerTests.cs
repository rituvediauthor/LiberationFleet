using FluentAssertions;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto.Queries.GetFleetKeyState;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Crypto.Queries.GetFleetKeyState;

public class GetFleetKeyStateQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenHistoricalVersionsExist_ReturnsAllMyWraps()
    {
        var fleetRepository = HandlerTestFixture.CreateFleetRepositoryMock();
        fleetRepository
            .Setup(r => r.IsUserInFleetAsync(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var historical = new[]
        {
            new FleetKeyDistribution
            {
                FleetId = 20,
                UserId = 1,
                KeyVersion = 3,
                WrappedFleetKey = "wrap-v3",
                WrapNonce = "nonce-v3",
                WrappedByUserId = 2
            },
            new FleetKeyDistribution
            {
                FleetId = 20,
                UserId = 1,
                KeyVersion = 1,
                WrappedFleetKey = "wrap-v1",
                WrapNonce = "nonce-v1",
                WrappedByUserId = 2
            }
        };

        var cryptoRepository = new Mock<ICryptoRepository>(MockBehavior.Strict);
        cryptoRepository
            .Setup(r => r.GetLatestFleetKeyVersionAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        cryptoRepository
            .Setup(r => r.GetFleetKeyDistributionsAsync(20, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { historical[0] });
        cryptoRepository
            .Setup(r => r.GetFleetKeyDistributionsForUserAsync(20, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(historical);

        var handler = new GetFleetKeyStateQueryHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(1).Object,
            fleetRepository.Object,
            cryptoRepository.Object);

        var result = await handler.Handle(new GetFleetKeyStateQuery(20), CancellationToken.None);

        result.Should().NotBeNull();
        result!.LatestKeyVersion.Should().Be(3);
        result.MyHistoricalDistributions.Should().HaveCount(2);
        result.MyHistoricalDistributions.Should().Contain(d => d.KeyVersion == 1 && d.WrappedFleetKey == "wrap-v1");
        cryptoRepository.VerifyAll();
    }
}
