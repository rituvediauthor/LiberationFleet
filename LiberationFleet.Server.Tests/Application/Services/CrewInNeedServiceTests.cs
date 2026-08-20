using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Services;

public class CrewInNeedServiceTests
{
    [Theory]
    [InlineData(10, 20, true, false)]
    [InlineData(20, 20, true, false)]
    [InlineData(21, 20, false, true)]
    public void InNeedThreshold_ComparesAverageToThreshold(
        decimal average,
        decimal threshold,
        bool expectedForcedInNeed,
        bool expectedCanToggleOff)
    {
        CrewInNeedService.IsAtOrBelowInNeedThreshold(average, threshold).Should().Be(expectedForcedInNeed);
        CrewInNeedService.CanToggleInNeedOff(average, threshold).Should().Be(expectedCanToggleOff);
    }

    [Fact]
    public async Task ApplyInNeedDefaultAsync_WhenAtOrBelowThreshold_ForcesInNeedTrue()
    {
        var (context, user, crew) = await TestDbContextFactory.CreateWithCrewAsync();
        await using (context)
        {
            crew.InNeedDefaultThreshold = 50m;
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
    public async Task ApplyInNeedDefaultAsync_WhenAboveThreshold_LeavesInNeedUnchanged()
    {
        var (context, user, crew) = await TestDbContextFactory.CreateWithCrewAsync();
        await using (context)
        {
            crew.InNeedDefaultThreshold = 0m;
            user.InNeedOfAid = false;
            var platforms = await TestDbContextFactory.SeedCrewPaymentPlatformsAsync(context, crew.Id);
            context.Gifts.Add(new Gift
            {
                CrewId = crew.Id,
                GiverUserId = user.Id,
                RecipientUserId = user.Id,
                Type = Domain.Enums.GiftType.Direct,
                Amount = 30m,
                CountsTowardContribution = true,
                CountsTowardReception = false,
                VerificationStatus = Domain.Enums.GiftVerificationStatus.Verified,
                CreatedAt = DateTime.UtcNow
            });
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
