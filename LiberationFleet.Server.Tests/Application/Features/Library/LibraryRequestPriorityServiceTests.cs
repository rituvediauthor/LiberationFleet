using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Library;

public class LibraryRequestPriorityServiceTests
{
    [Fact]
    public async Task ApplyPossessorPriority_WhenRequesterInSeason_ExcludesActiveSeasonContributionsAndRounds()
    {
        var mutualAid = new Mock<IMutualAidService>(MockBehavior.Strict);
        mutualAid
            .Setup(s => s.GetPriorityScoreForUserAsync(
                7,
                3,
                It.IsAny<CancellationToken>(),
                true))
            .ReturnsAsync(42.4m);

        var membershipRepository = new Mock<ICrewMembershipRepository>(MockBehavior.Strict);
        membershipRepository
            .Setup(r => r.GetActiveMembersByCrewIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CrewMembership>
            {
                new() { UserId = 7, CrewId = 3, IsInSeason = true, IsBanned = false }
            });

        var service = new LibraryRequestPriorityService(mutualAid.Object, membershipRepository.Object);
        var items = new List<LibraryRequestListItemDto>
        {
            new() { RequestId = 1, UnitId = 10, RequesterUserId = 7 }
        };
        var source = new List<LibraryRequest>
        {
            new()
            {
                Id = 1,
                UnitId = 10,
                RequesterUserId = 7,
                Status = LibraryRequestStatus.Open,
                NeededByStart = DateTime.UtcNow.AddDays(1),
                CreatedAt = DateTime.UtcNow
            }
        };

        await service.ApplyPossessorPriorityAsync(items, source, crewId: 3, CancellationToken.None);

        items[0].RequesterPriorityScore.Should().Be(42m);
        mutualAid.Verify(s => s.GetPriorityScoreForUserAsync(
            7,
            3,
            It.IsAny<CancellationToken>(),
            true), Times.Once);
    }

    [Fact]
    public async Task ApplyPossessorPriority_WhenRequesterNotInSeason_DoesNotExcludeActiveSeasonContributions()
    {
        var mutualAid = new Mock<IMutualAidService>(MockBehavior.Strict);
        mutualAid
            .Setup(s => s.GetPriorityScoreForUserAsync(
                7,
                3,
                It.IsAny<CancellationToken>(),
                false))
            .ReturnsAsync(10.6m);

        var membershipRepository = new Mock<ICrewMembershipRepository>(MockBehavior.Strict);
        membershipRepository
            .Setup(r => r.GetActiveMembersByCrewIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CrewMembership>
            {
                new() { UserId = 7, CrewId = 3, IsInSeason = false, IsBanned = false }
            });

        var service = new LibraryRequestPriorityService(mutualAid.Object, membershipRepository.Object);
        var items = new List<LibraryRequestListItemDto>
        {
            new() { RequestId = 1, UnitId = 10, RequesterUserId = 7 }
        };

        await service.ApplyPossessorPriorityAsync(items, Array.Empty<LibraryRequest>(), crewId: 3, CancellationToken.None);

        items[0].RequesterPriorityScore.Should().Be(11m);
        mutualAid.Verify(s => s.GetPriorityScoreForUserAsync(
            7,
            3,
            It.IsAny<CancellationToken>(),
            false), Times.Once);
    }
}
