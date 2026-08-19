using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Services;

public class CrewInNeedServiceTests
{
    [Theory]
    [InlineData(10, 20, true)]
    [InlineData(20, 20, false)]
    [InlineData(25, 20, false)]
    public void IsBelowContributionFloor_ComparesAverageToFloor(decimal average, decimal floor, bool expected)
    {
        CrewInNeedService.IsBelowContributionFloor(average, floor).Should().Be(expected);
        CrewInNeedService.CanToggleInNeedOff(average, floor).Should().Be(!expected);
    }

    [Fact]
    public async Task ApplyInNeedDefaultAsync_WhenBelowFloor_ForcesInNeedTrue()
    {
        var (context, user, crew) = await TestDbContextFactory.CreateWithCrewAsync();
        await using (context)
        {
            crew.FinancialMembershipContributionFloor = 50m;
            user.InNeedOfAid = false;
            await context.SaveChangesAsync();

            var changed = await CrewInNeedService.ApplyInNeedDefaultAsync(
                user.Id,
                new UserRepository(context),
                new GiftRepository(context),
                new CrewRepository(context),
                new CrewMembershipRepository(context),
                context,
                CancellationToken.None);

            changed.Should().BeTrue();
            (await context.Users.SingleAsync(u => u.Id == user.Id)).InNeedOfAid.Should().BeTrue();
        }
    }

    [Fact]
    public async Task ApplyInNeedDefaultAsync_WhenAtOrAboveFloor_LeavesInNeedUnchanged()
    {
        var (context, user, crew) = await TestDbContextFactory.CreateWithCrewAsync();
        await using (context)
        {
            crew.FinancialMembershipContributionFloor = 0m;
            user.InNeedOfAid = false;
            await context.SaveChangesAsync();

            var changed = await CrewInNeedService.ApplyInNeedDefaultAsync(
                user.Id,
                new UserRepository(context),
                new GiftRepository(context),
                new CrewRepository(context),
                new CrewMembershipRepository(context),
                context,
                CancellationToken.None);

            changed.Should().BeFalse();
            (await context.Users.SingleAsync(u => u.Id == user.Id)).InNeedOfAid.Should().BeFalse();
        }
    }
}
