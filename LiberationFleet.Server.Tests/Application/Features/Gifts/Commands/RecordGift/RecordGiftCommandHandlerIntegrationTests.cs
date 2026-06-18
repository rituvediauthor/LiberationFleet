using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGift;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts.Commands.RecordGift;

public class RecordGiftCommandHandlerIntegrationTests
{
    [Fact]
    public async Task Handle_WhenRecordingDirectGift_PersistsGiftToDatabase()
    {
        var (context, giver, crew) = await TestDbContextFactory.CreateWithCrewAsync();
        await using (context)
        {
            var recipient = new User
            {
                Username = "Ritu",
                Email = "ritu@example.com",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            context.Users.Add(recipient);
            await context.SaveChangesAsync();

            context.CrewMemberships.Add(new CrewMembership
            {
                UserId = recipient.Id,
                CrewId = crew.Id,
                IsBanned = false,
                JoinedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var membershipRepository = new CrewMembershipRepository(context);
            var giftRepository = new GiftRepository(context);
            var paymentPlatformRepository = new PaymentPlatformRepository(context);
            var mutualAidService = new MutualAidService(new MutualAidRepository(context), membershipRepository, context);
            var handler = new RecordGiftCommandHandler(
                HandlerTestFixture.CreateCurrentUserServiceMock(giver.Id).Object,
                membershipRepository,
                giftRepository,
                paymentPlatformRepository,
                mutualAidService,
                context);

            var result = await handler.Handle(
                new RecordGiftCommand(45, 1, recipient.Id, null, null),
                CancellationToken.None);

            result.Success.Should().BeTrue();

            var gifts = await context.Gifts
                .Include(g => g.GiverUser)
                .Include(g => g.RecipientUser)
                .Include(g => g.PaymentPlatform)
                .Where(g => g.CrewId == crew.Id)
                .ToListAsync();

            gifts.Should().ContainSingle();
            gifts[0].Type.Should().Be(GiftType.Direct);
            gifts[0].Amount.Should().Be(45);
            gifts[0].PaymentPlatform.Name.Should().Be("PayPal");
            gifts[0].GiverUserId.Should().Be(giver.Id);
            gifts[0].RecipientUserId.Should().Be(recipient.Id);
        }
    }

    [Fact]
    public async Task Handle_WhenCompletingInitiatedGift_PersistsCompletedGiftToDatabase()
    {
        var (context, giver, crew) = await TestDbContextFactory.CreateWithCrewAsync();
        await using (context)
        {
            var recipient = new User
            {
                Username = "Ritu",
                Email = "ritu@example.com",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            var middleman = new User
            {
                Username = "James",
                Email = "james@example.com",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            context.Users.AddRange(recipient, middleman);
            await context.SaveChangesAsync();

            context.CrewMemberships.AddRange(
                new CrewMembership { UserId = recipient.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow },
                new CrewMembership { UserId = middleman.Id, CrewId = crew.Id, IsBanned = false, JoinedAt = DateTime.UtcNow });
            await context.SaveChangesAsync();

            var initiated = new Gift
            {
                CrewId = crew.Id,
                GiverUserId = giver.Id,
                RecipientUserId = recipient.Id,
                MiddlemanUserId = middleman.Id,
                Type = GiftType.Initiated,
                Amount = 30,
                PaymentPlatformId = 2,
                CreatedAt = DateTime.UtcNow
            };
            context.Gifts.Add(initiated);
            await context.SaveChangesAsync();

            var membershipRepository = new CrewMembershipRepository(context);
            var giftRepository = new GiftRepository(context);
            var paymentPlatformRepository = new PaymentPlatformRepository(context);
            var mutualAidService = new MutualAidService(new MutualAidRepository(context), membershipRepository, context);
            var handler = new RecordGiftCommandHandler(
                HandlerTestFixture.CreateCurrentUserServiceMock(middleman.Id).Object,
                membershipRepository,
                giftRepository,
                paymentPlatformRepository,
                mutualAidService,
                context);

            var result = await handler.Handle(
                new RecordGiftCommand(30, 3, null, null, initiated.Id),
                CancellationToken.None);

            result.Success.Should().BeTrue();

            var completed = await context.Gifts
                .Include(g => g.PaymentPlatform)
                .Where(g => g.Type == GiftType.Completed)
                .SingleAsync();

            completed.InitiatedGiftId.Should().Be(initiated.Id);
            completed.Amount.Should().Be(30);
            completed.PaymentPlatform.Name.Should().Be("Venmo");
            completed.MiddlemanUserId.Should().Be(middleman.Id);
        }
    }
}
