using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Library;

public class LibraryRequestPriorityServiceTests
{
    [Fact]
    public async Task ApplyPossessorPriority_UsesLotAlignmentAndRoundsScore()
    {
        var mutualAid = new Mock<IMutualAidService>(MockBehavior.Strict);
        mutualAid
            .Setup(s => s.GetPriorityScoreForUserAsync(
                7,
                3,
                It.IsAny<CancellationToken>(),
                false,
                true))
            .ReturnsAsync(42.4m);

        var service = new LibraryRequestPriorityService(mutualAid.Object);
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
            false,
            true), Times.Once);
    }

    [Fact]
    public async Task ApplyPossessorPriority_RoundsFractionalScores()
    {
        var mutualAid = new Mock<IMutualAidService>(MockBehavior.Strict);
        mutualAid
            .Setup(s => s.GetPriorityScoreForUserAsync(
                7,
                3,
                It.IsAny<CancellationToken>(),
                false,
                true))
            .ReturnsAsync(10.6m);

        var service = new LibraryRequestPriorityService(mutualAid.Object);
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
            false,
            true), Times.Once);
    }
}
