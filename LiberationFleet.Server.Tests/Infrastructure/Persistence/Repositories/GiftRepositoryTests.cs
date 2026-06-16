using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;

namespace LiberationFleet.Server.Tests.Infrastructure.Persistence.Repositories;

public class GiftRepositoryTests
{
    [Fact]
    public async Task GetUserGiftStatsAsync_CountsDirectAndCompletedContributions()
    {
        var (context, giver, crew) = await TestDbContextFactory.CreateWithCrewAsync();
        await using (context)
        {
            var recipient = new User
            {
                Username = "recipient",
                Email = "recipient@example.com",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            context.Users.Add(recipient);
            await context.SaveChangesAsync();

            var now = DateTime.UtcNow;
            context.Gifts.AddRange(
                new Gift
                {
                    CrewId = crew.Id,
                    GiverUserId = giver.Id,
                    RecipientUserId = recipient.Id,
                    Type = GiftType.Direct,
                    Amount = 50,
                    PaymentPlatformId = 1,
                    CreatedAt = now.AddMonths(-1)
                },
                new Gift
                {
                    CrewId = crew.Id,
                    GiverUserId = giver.Id,
                    RecipientUserId = recipient.Id,
                    Type = GiftType.Direct,
                    Amount = 25,
                    PaymentPlatformId = 3,
                    CreatedAt = now.AddMonths(-2)
                },
                new Gift
                {
                    CrewId = crew.Id,
                    GiverUserId = giver.Id,
                    RecipientUserId = recipient.Id,
                    Type = GiftType.Initiated,
                    Amount = 100,
                    PaymentPlatformId = 1,
                    CreatedAt = now.AddMonths(-1)
                },
                new Gift
                {
                    CrewId = crew.Id,
                    GiverUserId = recipient.Id,
                    RecipientUserId = giver.Id,
                    Type = GiftType.Direct,
                    Amount = 10,
                    PaymentPlatformId = 1,
                    CreatedAt = now.AddMonths(-1)
                });
            await context.SaveChangesAsync();

            var repository = new GiftRepository(context);

            var stats = await repository.GetUserGiftStatsAsync(giver.Id);

            stats.LifetimeContributions.Should().Be(75);
            stats.SacrificeCountLastYear.Should().Be(2);
            stats.ContributionsLast3Months.Should().Be(75);
            stats.ReceptionLastYear.Should().Be(10);
        }
    }

    [Fact]
    public async Task GetUserGiftStatsAsync_ExcludesGiftsOutsideTimeWindows()
    {
        var (context, giver, crew) = await TestDbContextFactory.CreateWithCrewAsync();
        await using (context)
        {
            var recipient = new User
            {
                Username = "recipient",
                Email = "recipient@example.com",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            context.Users.Add(recipient);
            await context.SaveChangesAsync();

            var now = DateTime.UtcNow;
            context.Gifts.AddRange(
                new Gift
                {
                    CrewId = crew.Id,
                    GiverUserId = giver.Id,
                    RecipientUserId = recipient.Id,
                    Type = GiftType.Direct,
                    Amount = 30,
                    PaymentPlatformId = 1,
                    CreatedAt = now.AddYears(-2)
                },
                new Gift
                {
                    CrewId = crew.Id,
                    GiverUserId = giver.Id,
                    RecipientUserId = recipient.Id,
                    Type = GiftType.Direct,
                    Amount = 20,
                    PaymentPlatformId = 1,
                    CreatedAt = now.AddMonths(-5)
                });
            await context.SaveChangesAsync();

            var repository = new GiftRepository(context);

            var stats = await repository.GetUserGiftStatsAsync(giver.Id);

            stats.LifetimeContributions.Should().Be(50);
            stats.SacrificeCountLastYear.Should().Be(1);
            stats.ContributionsLast3Months.Should().Be(0);
        }
    }
}
