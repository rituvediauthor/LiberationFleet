using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.EmergencyRequests;
using LiberationFleet.Server.Domain.Entities;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.EmergencyRequests;

public class EmergencyRequestAccessTests
{
    [Fact]
    public async Task CanAccessAsync_WhenSameCrew_ReturnsTrue()
    {
        var fleetRepository = new Mock<IFleetRepository>(MockBehavior.Strict);

        var result = await EmergencyRequestAccess.CanAccessAsync(
            fleetRepository.Object,
            viewerCrewId: 5,
            requestCrewId: 5,
            CancellationToken.None);

        result.Should().BeTrue();
        fleetRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CanAccessAsync_WhenSameFleet_ReturnsTrue()
    {
        var fleetRepository = new Mock<IFleetRepository>();
        fleetRepository
            .Setup(r => r.GetFleetForCrewAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Fleet { Id = 10, Name = "Fleet" });
        fleetRepository
            .Setup(r => r.IsCrewInFleetAsync(2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await EmergencyRequestAccess.CanAccessAsync(
            fleetRepository.Object,
            viewerCrewId: 1,
            requestCrewId: 2,
            CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessAsync_WhenDifferentFleet_ReturnsFalse()
    {
        var fleetRepository = new Mock<IFleetRepository>();
        fleetRepository
            .Setup(r => r.GetFleetForCrewAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Fleet { Id = 10, Name = "Fleet" });
        fleetRepository
            .Setup(r => r.IsCrewInFleetAsync(2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await EmergencyRequestAccess.CanAccessAsync(
            fleetRepository.Object,
            viewerCrewId: 1,
            requestCrewId: 2,
            CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessAsync_WhenViewerCrewNotInFleet_ReturnsFalse()
    {
        var fleetRepository = new Mock<IFleetRepository>();
        fleetRepository
            .Setup(r => r.GetFleetForCrewAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Fleet?)null);

        var result = await EmergencyRequestAccess.CanAccessAsync(
            fleetRepository.Object,
            viewerCrewId: 1,
            requestCrewId: 2,
            CancellationToken.None);

        result.Should().BeFalse();
    }
}
