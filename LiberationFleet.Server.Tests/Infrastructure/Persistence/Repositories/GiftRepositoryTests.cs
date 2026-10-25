using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;

namespace LiberationFleet.Server.Tests.Infrastructure.Persistence.Repositories;

public class GiftRepositoryTests
{
    [Fact]
    public async Task GetLogPageByCrewIdAsync_ReturnsNewestEntriesInAscendingOrder()
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

            var platform = new CrewPaymentPlatform { CrewId = crew.Id, Name = "PayPal" };
            context.CrewPaymentPlatforms.Add(platform);
            await context.SaveChangesAsync();

            var baseTime = DateTime.UtcNow.AddDays(-5);
            for (var i = 1; i <= 55; i++)
            {
                context.Gifts.Add(new Gift
                {
                    CrewId = crew.Id,
                    GiverUserId = giver.Id,
                    RecipientUserId = recipient.Id,
                    Type = GiftType.Direct,
                    Amount = i,
                    CrewPaymentPlatformId = platform.Id,
                    CreatedAt = baseTime.AddMinutes(i)
                });
            }
            await context.SaveChangesAsync();

            var repository = new GiftRepository(context);
            var firstPage = await repository.GetLogPageByCrewIdAsync(crew.Id, 50);

            firstPage.HasMore.Should().BeTrue();
            firstPage.Items.Should().HaveCount(50);
            firstPage.Items.First().Amount.Should().Be(6);
            firstPage.Items.Last().Amount.Should().Be(55);

            var oldest = firstPage.Items.First();
            var secondPage = await repository.GetLogPageByCrewIdAsync(
                crew.Id,
                50,
                oldest.CreatedAt,
                oldest.Id);

            secondPage.HasMore.Should().BeFalse();
            secondPage.Items.Should().HaveCount(5);
            secondPage.Items.First().Amount.Should().Be(1);
            secondPage.Items.Last().Amount.Should().Be(5);
        }
    }

    [Fact]
    public async Task GetGiverRecipientSummariesAsync_GroupsOutgoingGiftsByRecipient()
    {
        var (context, giver, crew) = await TestDbContextFactory.CreateWithCrewAsync();
        await using (context)
        {
            var recipientA = new User
            {
                Username = "alice",
                Email = "alice@example.com",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            var recipientB = new User
            {
                Username = "bob",
                Email = "bob@example.com",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            context.Users.AddRange(recipientA, recipientB);
            await context.SaveChangesAsync();

            var platform = new CrewPaymentPlatform { CrewId = crew.Id, Name = "PayPal" };
            context.CrewPaymentPlatforms.Add(platform);
            await context.SaveChangesAsync();

            var middleman = new User
            {
                Username = "middle",
                Email = "middle@example.com",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            context.Users.Add(middleman);
            await context.SaveChangesAsync();

            context.Gifts.AddRange(
                new Gift
                {
                    CrewId = crew.Id,
                    GiverUserId = giver.Id,
                    RecipientUserId = recipientA.Id,
                    Type = GiftType.Direct,
                    Amount = 25,
                    CrewPaymentPlatformId = platform.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                },
                new Gift
                {
                    CrewId = crew.Id,
                    GiverUserId = giver.Id,
                    RecipientUserId = recipientB.Id,
                    Type = GiftType.Initiated,
                    MiddlemanUserId = middleman.Id,
                    Amount = 40,
                    CrewPaymentPlatformId = platform.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                });

            await context.SaveChangesAsync();

            var repository = new GiftRepository(context);
            var summaries = await repository.GetGiverRecipientSummariesAsync(giver.Id);

            summaries.Should().HaveCount(2);
            summaries.Should().Contain(s => s.RecipientUserId == recipientA.Id && s.TotalAmount == 25 && s.GiftCount == 1);
            summaries.Should().Contain(s => s.RecipientUserId == recipientB.Id && s.TotalAmount == 40 && s.GiftCount == 1);
            summaries[0].RecipientUserId.Should().Be(recipientB.Id);
        }
    }

    [Fact]
    public async Task GetActiveLikeCountsForGiftsAsync_CountsMultipleLikesAndIgnoresRemoved()
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
            var liker = new User
            {
                Username = "liker",
                Email = "liker@example.com",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            context.Users.AddRange(recipient, liker);
            await context.SaveChangesAsync();

            var platform = new CrewPaymentPlatform { CrewId = crew.Id, Name = "PayPal" };
            context.CrewPaymentPlatforms.Add(platform);
            await context.SaveChangesAsync();

            var popular = new Gift
            {
                CrewId = crew.Id,
                GiverUserId = giver.Id,
                RecipientUserId = recipient.Id,
                Type = GiftType.Direct,
                Amount = 20,
                CrewPaymentPlatformId = platform.Id,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2)
            };
            var quiet = new Gift
            {
                CrewId = crew.Id,
                GiverUserId = giver.Id,
                RecipientUserId = recipient.Id,
                Type = GiftType.Direct,
                Amount = 10,
                CrewPaymentPlatformId = platform.Id,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            };
            context.Gifts.AddRange(popular, quiet);
            await context.SaveChangesAsync();

            context.GiftLikes.AddRange(
                new GiftLike { UserId = giver.Id, GiftId = popular.Id, CreatedAt = DateTime.UtcNow.AddMinutes(-3) },
                new GiftLike { UserId = liker.Id, GiftId = popular.Id, CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
                new GiftLike
                {
                    UserId = recipient.Id,
                    GiftId = popular.Id,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-4),
                    RemovedAt = DateTime.UtcNow.AddMinutes(-1)
                },
                new GiftLike { UserId = giver.Id, GiftId = quiet.Id, CreatedAt = DateTime.UtcNow.AddMinutes(-1) });
            await context.SaveChangesAsync();

            var repository = new GiftRepository(context);
            var counts = await repository.GetActiveLikeCountsForGiftsAsync([popular.Id, quiet.Id]);
            var likedByGiver = await repository.GetActiveLikedGiftIdsByUserAsync(giver.Id, [popular.Id, quiet.Id]);
            var likedByRecipient = await repository.GetActiveLikedGiftIdsByUserAsync(recipient.Id, [popular.Id, quiet.Id]);

            counts[popular.Id].Should().Be(2);
            counts[quiet.Id].Should().Be(1);
            likedByGiver.Should().BeEquivalentTo([popular.Id, quiet.Id]);
            likedByRecipient.Should().BeEmpty();
        }
    }
}
