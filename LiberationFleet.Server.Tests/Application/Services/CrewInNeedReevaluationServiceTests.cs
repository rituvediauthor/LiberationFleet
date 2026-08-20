using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using IMutualAidService = LiberationFleet.Server.Application.Common.Interfaces.IMutualAidService;

namespace LiberationFleet.Server.Tests.Application.Services;

public class CrewInNeedReevaluationServiceTests
{
    [Fact]
    public async Task ReevaluateCrewAsync_WhenThresholdRises_ForcesAffectedCrewmatesInNeed()
    {
        var (context, user, crew) = await TestDbContextFactory.CreateWithCrewAsync();
        await using (context)
        {
            crew.InNeedDefaultThreshold = 100m;
            user.InNeedOfAid = false;
            await context.SaveChangesAsync();

            var mutualAidMock = new Mock<IMutualAidService>();
            mutualAidMock
                .Setup(m => m.OnInNeedOfAidChangedAsync(user.Id, true, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = new CrewInNeedReevaluationService(
                new CrewMembershipRepository(context),
                new UserRepository(context),
                new GiftRepository(context),
                new CrewRepository(context),
                mutualAidMock.Object,
                context);

            await service.ReevaluateCrewAsync(crew.Id, CancellationToken.None);

            (await context.Users.SingleAsync(u => u.Id == user.Id)).InNeedOfAid.Should().BeTrue();
            mutualAidMock.Verify(
                m => m.OnInNeedOfAidChangedAsync(user.Id, true, It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
