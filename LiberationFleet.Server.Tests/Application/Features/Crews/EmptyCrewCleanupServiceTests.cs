using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Features.Crews;

public class EmptyCrewCleanupServiceTests
{
    [Fact]
    public async Task TryCleanupIfNoActiveMembers_DeletesCrewDataButPreservesGifts()
    {
        var (context, user, crew) = await TestDbContextFactory.CreateWithCrewAsync();
        var platforms = await TestDbContextFactory.SeedCrewPaymentPlatformsAsync(context, crew.Id);

        context.CrewRules.Add(new CrewRule
        {
            CrewId = crew.Id,
            CreatedByUserId = user.Id,
            Title = "Rule",
            CreatedAt = DateTime.UtcNow
        });
        context.ChatRooms.Add(new ChatRoom
        {
            CrewId = crew.Id,
            CreatedByUserId = user.Id,
            Name = "General",
            Purpose = "Chat",
            CreatedAt = DateTime.UtcNow
        });
        context.Gifts.Add(new Gift
        {
            CrewId = crew.Id,
            GiverUserId = user.Id,
            RecipientUserId = user.Id,
            CrewPaymentPlatformId = platforms["PayPal"].Id,
            Amount = 25m,
            Type = GiftType.Direct
        });
        await context.SaveChangesAsync();

        var membershipRepository = new CrewMembershipRepository(context);
        var cleanupRepository = new CrewCleanupRepository(context);
        var service = new EmptyCrewCleanupService(membershipRepository, cleanupRepository);

        membershipRepository.Remove(context.CrewMemberships.Single());
        await context.SaveChangesAsync();
        await service.TryCleanupIfNoActiveMembersAsync(crew.Id);
        await context.SaveChangesAsync();

        Assert.False(await context.CrewMemberships.AnyAsync());
        Assert.False(await context.CrewRules.AnyAsync());
        Assert.False(await context.ChatRooms.AnyAsync());
        Assert.True(await context.Gifts.AnyAsync());
        Assert.True(await context.Crews.AnyAsync(c => c.Id == crew.Id));
        Assert.True(await context.CrewPaymentPlatforms.AnyAsync(p => p.CrewId == crew.Id));
    }

    [Fact]
    public async Task TryCleanupIfNoActiveMembers_DeletesCrewWhenNoGiftsExist()
    {
        var (context, _, crew) = await TestDbContextFactory.CreateWithCrewAsync();
        await TestDbContextFactory.SeedCrewPaymentPlatformsAsync(context, crew.Id);

        var membershipRepository = new CrewMembershipRepository(context);
        var cleanupRepository = new CrewCleanupRepository(context);
        var service = new EmptyCrewCleanupService(membershipRepository, cleanupRepository);

        membershipRepository.Remove(context.CrewMemberships.Single());
        await context.SaveChangesAsync();
        await service.TryCleanupIfNoActiveMembersAsync(crew.Id);
        await context.SaveChangesAsync();

        Assert.False(await context.Crews.AnyAsync());
        Assert.False(await context.CrewPaymentPlatforms.AnyAsync());
    }

    [Fact]
    public async Task TryCleanupIfNoActiveMembers_DoesNothingWhileActiveMembersRemain()
    {
        var (context, user, crew) = await TestDbContextFactory.CreateWithCrewAsync();

        var secondUser = new User
        {
            Username = "second",
            Email = "second@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        context.Users.Add(secondUser);
        await context.SaveChangesAsync();

        context.CrewMemberships.Add(new CrewMembership
        {
            UserId = secondUser.Id,
            CrewId = crew.Id,
            JoinedAt = DateTime.UtcNow
        });
        context.CrewRules.Add(new CrewRule
        {
            CrewId = crew.Id,
            CreatedByUserId = user.Id,
            Title = "Rule",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var membershipRepository = new CrewMembershipRepository(context);
        var cleanupRepository = new CrewCleanupRepository(context);
        var service = new EmptyCrewCleanupService(membershipRepository, cleanupRepository);

        membershipRepository.Remove(context.CrewMemberships.First(m => m.UserId == user.Id));
        await context.SaveChangesAsync();
        await service.TryCleanupIfNoActiveMembersAsync(crew.Id);
        await context.SaveChangesAsync();

        Assert.True(await context.CrewRules.AnyAsync());
        Assert.Equal(1, await context.CrewMemberships.CountAsync());
    }
}
