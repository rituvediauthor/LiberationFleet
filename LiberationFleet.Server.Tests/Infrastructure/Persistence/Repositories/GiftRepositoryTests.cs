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
}
