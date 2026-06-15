using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Queries.SearchCrews;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Geocoding;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Crews.Queries.SearchCrews;

public class SearchCrewsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenUserIsNotAuthenticated_ReturnsUnauthorized()
    {
        var handler = CreateHandler(currentUserId: null);

        var result = await handler.Handle(new SearchCrewsQuery { Scope = "Online" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task Handle_WhenOnlineSearch_FiltersBannedAndFullCrews()
    {
        var userId = 2;
        var availableCrew = HandlerTestFixture.CreateCrew(id: 1, name: "Alpha");
        var fullCrew = HandlerTestFixture.CreateCrew(id: 2, name: "Beta", maxSize: 1);
        var bannedCrew = HandlerTestFixture.CreateCrew(id: 3, name: "Gamma");

        var crewRepository = HandlerTestFixture.CreateCrewRepositoryMock();
        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();

        crewRepository
            .Setup(r => r.SearchPublicAsync(CrewScope.Online, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Crew> { availableCrew, fullCrew, bannedCrew });

        membershipRepository
            .Setup(r => r.IsUserBannedFromCrewAsync(userId, availableCrew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        membershipRepository
            .Setup(r => r.IsUserBannedFromCrewAsync(userId, fullCrew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        membershipRepository
            .Setup(r => r.IsUserBannedFromCrewAsync(userId, bannedCrew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        crewRepository
            .Setup(r => r.CountMembersAsync(availableCrew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        crewRepository
            .Setup(r => r.CountMembersAsync(fullCrew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler(currentUserId: userId, crewRepository: crewRepository, membershipRepository: membershipRepository);

        var result = await handler.Handle(new SearchCrewsQuery { Scope = "Online", Page = 1, PageSize = 10 }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().ContainSingle(c => c.Name == "Alpha");
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenLocalSearch_FiltersByDistance()
    {
        var userId = 2;
        var nearbyCrew = HandlerTestFixture.CreateCrew(id: 1, name: "Nearby", scope: CrewScope.Local, zipCode: "10002");
        var farCrew = HandlerTestFixture.CreateCrew(id: 2, name: "Far", scope: CrewScope.Local, zipCode: "90210");

        var crewRepository = HandlerTestFixture.CreateCrewRepositoryMock();
        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        var zipService = new ZipCodeDistanceService();

        crewRepository
            .Setup(r => r.SearchPublicAsync(CrewScope.Local, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Crew> { nearbyCrew, farCrew });

        membershipRepository
            .Setup(r => r.IsUserBannedFromCrewAsync(userId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        crewRepository
            .Setup(r => r.CountMembersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler(currentUserId: userId, crewRepository: crewRepository, membershipRepository: membershipRepository, zipService: zipService);

        var result = await handler.Handle(new SearchCrewsQuery
        {
            Scope = "Local",
            ZipCode = "10001",
            RadiusMiles = 5,
            Page = 1,
            PageSize = 10
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().ContainSingle(c => c.Name == "Nearby");
        result.Items[0].DistanceMiles.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenNoCrewsMatch_ReturnsEmptyResult()
    {
        var crewRepository = HandlerTestFixture.CreateCrewRepositoryMock();
        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();

        crewRepository
            .Setup(r => r.SearchPublicAsync(CrewScope.Online, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Crew>());

        var handler = CreateHandler(crewRepository: crewRepository, membershipRepository: membershipRepository);

        var result = await handler.Handle(new SearchCrewsQuery { Scope = "Online" }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().BeEmpty();
        result.Message.Should().Be("No crews found matching your search");
        result.TotalPages.Should().Be(0);
    }

    private static SearchCrewsQueryHandler CreateHandler(
        int? currentUserId = 1,
        Mock<ICrewRepository>? crewRepository = null,
        Mock<ICrewMembershipRepository>? membershipRepository = null,
        IZipCodeDistanceService? zipService = null)
    {
        crewRepository ??= HandlerTestFixture.CreateCrewRepositoryMock();
        membershipRepository ??= HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        zipService ??= new ZipCodeDistanceService();

        return new SearchCrewsQueryHandler(
            crewRepository.Object,
            membershipRepository.Object,
            zipService,
            HandlerTestFixture.CreateCurrentUserServiceMock(currentUserId).Object);
    }
}
