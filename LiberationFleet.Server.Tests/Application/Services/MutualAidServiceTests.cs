using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Services;

public class MutualAidServiceTests
{
    [Fact]
    public void FindMiddlemen_WhenGiverAndRecipientSharePlatform_ReturnsEmpty()
    {
        var members = new List<CrewMemberPlatforms>
        {
            new() { UserId = 1, Username = "giver", PlatformIds = [1, 2] },
            new() { UserId = 2, Username = "recipient", PlatformIds = [2] },
            new() { UserId = 3, Username = "middle", PlatformIds = [1, 2] }
        };
        var service = CreateService();

        service.FindMiddlemen(1, 2, members).Should().BeEmpty();
    }

    [Fact]
    public void FindMiddlemen_WhenNoDirectOverlap_ReturnsSharedMiddleman()
    {
        var members = CreateMemberPlatforms();
        var service = CreateService();

        service.FindMiddlemen(1, 2, members).Should().BeEquivalentTo([3]);
    }

    [Fact]
    public void FindMiddlemen_ExcludesGiverAndRecipientFromCandidates()
    {
        var members = new List<CrewMemberPlatforms>
        {
            new() { UserId = 1, Username = "giver", PlatformIds = [1] },
            new() { UserId = 2, Username = "recipient", PlatformIds = [2] }
        };
        var service = CreateService();

        service.FindMiddlemen(1, 2, members).Should().BeEmpty();
    }

    [Fact]
    public async Task GetReceptionOrderAsync_ExcludesSelfWhenRequested()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        var order = await fixture.Service.GetReceptionOrderAsync(
            fixture.Bob.Id,
            excludeSelfAsRecipient: true,
            cancellationToken: CancellationToken.None);

        order.Should().NotBeEmpty();
        order.Should().NotContain(e => e.UserId == fixture.Bob.Id);
        order[0].UserId.Should().Be(fixture.Alice.Id);
    }

    [Fact]
    public async Task GetReceptionOrderAsync_IncludesSelfWhenExcludedFlagIsFalse()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        var order = await fixture.Service.GetReceptionOrderAsync(
            fixture.Bob.Id,
            excludeSelfAsRecipient: false,
            cancellationToken: CancellationToken.None);

        order[0].UserId.Should().Be(fixture.Bob.Id);
    }

    [Fact]
    public async Task GetReceptionOrderAsync_ReturnsEmptyWhenGiverNotInSeason()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        await fixture.SetInSeasonAsync(fixture.Alice, isInSeason: false);

        var order = await fixture.Service.GetReceptionOrderAsync(
            fixture.Alice.Id,
            requireGiverInSeason: true,
            cancellationToken: CancellationToken.None);

        order.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReceptionOrderAsync_PopulatesMiddlemanOptionsWhenPlatformsDoNotOverlap()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        var order = await fixture.Service.GetReceptionOrderAsync(
            fixture.Alice.Id,
            cancellationToken: CancellationToken.None);

        var bobEntry = order.First(e => e.UserId == fixture.Bob.Id);
        bobEntry.CommonPlatformIds.Should().BeEmpty();
        bobEntry.NoSuitableMiddleman.Should().BeFalse();
        bobEntry.MiddlemanOptions.Should().ContainSingle();
        bobEntry.MiddlemanOptions[0].UserId.Should().Be(fixture.Carol.Id);
        bobEntry.MiddlemanOptions[0].CommonPlatformIds.Should().Contain(fixture.Platforms["PayPal"].Id);
        bobEntry.MiddlemanOptions[0].PlatformAccounts.Should().Contain(p =>
            p.PlatformId == fixture.Platforms["PayPal"].Id && p.Handle == "@carol-paypal");
    }

    [Fact]
    public async Task GetReceptionOrderAsync_PopulatesDirectCommonPlatformsAndRecipientAccounts()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        var order = await fixture.Service.GetReceptionOrderAsync(
            fixture.Alice.Id,
            cancellationToken: CancellationToken.None);

        var carolEntry = order.First(e => e.UserId == fixture.Carol.Id);
        carolEntry.CommonPlatformIds.Should().Contain(fixture.Platforms["PayPal"].Id);
        carolEntry.RecipientPreferredPlatformName.Should().Be("Venmo");
        carolEntry.RecipientPreferredPlatformHandle.Should().Be("@carol-venmo");
        carolEntry.RecipientPlatformAccounts.Should().Contain(p =>
            p.PlatformId == fixture.Platforms["PayPal"].Id && p.Handle == "@carol-paypal");
    }

    [Fact]
    public async Task GetReceptionOrderAsync_PlacesThresholdsBeforeCycles()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        await fixture.AddUnsatisfiedThresholdAsync(fixture.Carol, thresholdAmount: 50m);

        var order = await fixture.Service.GetReceptionOrderAsync(
            fixture.Alice.Id,
            cancellationToken: CancellationToken.None);

        order[0].EntryType.Should().Be("survivalThreshold");
        order[0].UserId.Should().Be(fixture.Carol.Id);
        order[1].EntryType.Should().Be("cycle");
    }

    [Fact]
    public async Task GetReceptionOrderAsync_WhenSurvivalThresholdsDisabled_ExcludesThresholdEntries()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        fixture.Crew.AllowSurvivalThresholds = false;
        await fixture.Context.SaveChangesAsync();
        await fixture.AddUnsatisfiedThresholdAsync(fixture.Carol, thresholdAmount: 50m);

        var order = await fixture.Service.GetReceptionOrderAsync(
            fixture.Alice.Id,
            cancellationToken: CancellationToken.None);

        order.Should().NotContain(e => e.EntryType == "survivalThreshold");
        order.Should().OnlyContain(e => e.EntryType == "cycle");
    }

    [Fact]
    public async Task SimulateNewMonthAsync_WhenSurvivalThresholdsDisabled_DoesNotCreateThresholds()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        fixture.Crew.AllowSurvivalThresholds = false;
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.SimulateNewMonthAsync(fixture.Alice.Id, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("disabled");
        (await fixture.Context.MonthlySurvivalThresholds.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetNextAidAsync_WhenUserIsNextRecipient_SetsIsCurrentUserRecipient()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        var nextAid = await fixture.Service.GetNextAidAsync(fixture.Bob.Id, CancellationToken.None);

        nextAid.Should().NotBeNull();
        nextAid!.RecipientName.Should().Be("bob");
        nextAid.Amount.Should().Be(600m);
        nextAid.IsCurrentUserRecipient.Should().BeTrue();
        nextAid.PlatformDisplayKind.Should().Be(NextAidPlatformDisplayKind.None);
    }

    [Fact]
    public async Task GetNextAidAsync_WhenAnotherCrewmateIsNext_DoesNotMarkCurrentUserAsRecipient()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        var nextAid = await fixture.Service.GetNextAidAsync(fixture.Alice.Id, CancellationToken.None);

        nextAid.Should().NotBeNull();
        nextAid!.RecipientName.Should().Be("bob");
        nextAid.IsCurrentUserRecipient.Should().BeFalse();
        nextAid.PlatformDisplayKind.Should().Be(NextAidPlatformDisplayKind.MiddlemanNeeded);
    }

    [Fact]
    public async Task GetNextAidAsync_WhenSharedPreferredPlatform_UsesPreferredDisplay()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        var nextAid = await fixture.Service.GetNextAidAsync(fixture.Carol.Id, CancellationToken.None);

        nextAid.Should().NotBeNull();
        nextAid!.RecipientName.Should().Be("bob");
        nextAid.PlatformDisplayKind.Should().Be(NextAidPlatformDisplayKind.Preferred);
        nextAid.PlatformName.Should().Be("Venmo");
        nextAid.PlatformHandle.Should().Be("@bob-venmo");
    }

    [Fact]
    public async Task GetNextAidAsync_WhenSharedNonPreferredPlatform_UsesCommonDisplay()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        await fixture.AddUnsatisfiedThresholdAsync(fixture.Carol, thresholdAmount: 50m);

        var nextAid = await fixture.Service.GetNextAidAsync(fixture.Alice.Id, CancellationToken.None);

        nextAid.Should().NotBeNull();
        nextAid!.RecipientName.Should().Be("carol");
        nextAid.PlatformDisplayKind.Should().Be(NextAidPlatformDisplayKind.Common);
        nextAid.PlatformName.Should().Be("PayPal");
        nextAid.PlatformHandle.Should().Be("@carol-paypal");
    }

    [Fact]
    public async Task GetNextAidAsync_ReturnsNullWhenSeasonNotStarted()
    {
        var (context, user, crew) = await TestDbContextFactory.CreateWithCrewAsync();
        await using (context)
        {
            var membershipRepository = new CrewMembershipRepository(context);
            var service = new MutualAidService(
                new MutualAidRepository(context),
                membershipRepository,
                context);

            var result = await service.GetNextAidAsync(user.Id, CancellationToken.None);
            result.Should().BeNull();
        }
    }

    [Fact]
    public async Task ApplyGiftReceptionAsync_UpdatesRecipientCycleReceived()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        var gift = new Gift
        {
            CrewId = fixture.Crew.Id,
            GiverUserId = fixture.Alice.Id,
            RecipientUserId = fixture.Bob.Id,
            Type = GiftType.Direct,
            Amount = 125m,
            CrewPaymentPlatformId = fixture.Platforms["PayPal"].Id,
            CountsTowardReception = true,
            IsCustomGift = false,
            IsSurvivalThreshold = false,
            CreatedAt = DateTime.UtcNow
        };

        fixture.Context.Gifts.Add(gift);
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.ApplyGiftReceptionAsync(gift, CancellationToken.None);

        var cycle = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.CrewId == fixture.Crew.Id && c.UserId == fixture.Bob.Id);
        cycle.CycleReceived.Should().Be(125m);
        cycle.TotalReceptionAmount.Should().Be(125m);
        cycle.HasCycleStarted.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyGiftReceptionAsync_SkipsCustomGifts()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        var gift = new Gift
        {
            CrewId = fixture.Crew.Id,
            GiverUserId = fixture.Alice.Id,
            RecipientUserId = fixture.Bob.Id,
            Type = GiftType.Direct,
            Amount = 50m,
            CrewPaymentPlatformId = fixture.Platforms["PayPal"].Id,
            CountsTowardReception = true,
            IsCustomGift = true,
            CreatedAt = DateTime.UtcNow
        };

        fixture.Context.Gifts.Add(gift);
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.ApplyGiftReceptionAsync(gift, CancellationToken.None);

        var cycle = await fixture.Context.SeasonCycles.SingleAsync(c => c.UserId == fixture.Bob.Id);
        cycle.CycleReceived.Should().Be(0m);
        cycle.TotalReceptionAmount.Should().Be(0m);
    }

    [Fact]
    public async Task ApplyGiftReceptionAsync_AppliesSurvivalThresholdGiftToThreshold()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var threshold = await fixture.AddUnsatisfiedThresholdAsync(fixture.Bob, thresholdAmount: 40m);

        var gift = new Gift
        {
            CrewId = fixture.Crew.Id,
            GiverUserId = fixture.Alice.Id,
            RecipientUserId = fixture.Bob.Id,
            Type = GiftType.Direct,
            Amount = 25m,
            CrewPaymentPlatformId = fixture.Platforms["PayPal"].Id,
            CountsTowardReception = true,
            IsCustomGift = false,
            IsSurvivalThreshold = true,
            CreatedAt = DateTime.UtcNow
        };

        fixture.Context.Gifts.Add(gift);
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.ApplyGiftReceptionAsync(gift, CancellationToken.None);

        var reloaded = await fixture.Context.MonthlySurvivalThresholds.SingleAsync(t => t.Id == threshold.Id);
        reloaded.ReceivedAmount.Should().Be(25m);
        reloaded.Satisfied.Should().BeFalse();
    }

    [Fact]
    public async Task MarkSeasonReadyAsync_StartsSeasonWhenThreeMembersReady()
    {
        var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedPaymentPlatformsAsync(context);

        var users = new[] { "u1", "u2", "u3" }
            .Select(name => new User
            {
                Username = name,
                Email = $"{name}@example.com",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            })
            .ToList();
        context.Users.AddRange(users);
        await context.SaveChangesAsync();

        var crew = HandlerTestFixture.CreateCrew(createdByUserId: users[0].Id);
        crew.SeasonStarted = false;
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var platforms = await TestDbContextFactory.SeedCrewPaymentPlatformsAsync(context, crew.Id);
        foreach (var user in users)
        {
            context.CrewMemberships.Add(new CrewMembership
            {
                UserId = user.Id,
                CrewId = crew.Id,
                EstimatedMonthlyContribution = 100m,
                IsSeasonReady = true,
                JoinedAt = DateTime.UtcNow
            });
            context.UserPaymentPlatforms.Add(new UserPaymentPlatform
            {
                UserId = user.Id,
                CrewPaymentPlatformId = platforms["PayPal"].Id,
                Handle = $"@{user.Username}"
            });
        }
        await context.SaveChangesAsync();

        var membershipRepository = new CrewMembershipRepository(context);
        var service = new MutualAidService(
            new MutualAidRepository(context),
            membershipRepository,
            context);

        var result = await service.MarkSeasonReadyAsync(users[2].Id, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SeasonStarted.Should().BeTrue();

        var reloadedCrew = await context.Crews.SingleAsync(c => c.Id == crew.Id);
        reloadedCrew.SeasonStarted.Should().BeTrue();
        (await context.SeasonCycles.CountAsync(c => c.CrewId == crew.Id)).Should().Be(3);
    }

    private static IReadOnlyList<CrewMemberPlatforms> CreateMemberPlatforms() =>
    [
        new CrewMemberPlatforms { UserId = 1, Username = "giver", PlatformIds = [1] },
        new CrewMemberPlatforms { UserId = 2, Username = "recipient", PlatformIds = [2] },
        new CrewMemberPlatforms { UserId = 3, Username = "middle", PlatformIds = [1, 2] }
    ];

    private static MutualAidService CreateService()
    {
        var context = TestDbContextFactory.Create();
        var membershipRepository = new CrewMembershipRepository(context);
        return new MutualAidService(
            new MutualAidRepository(context),
            membershipRepository,
            context);
    }
}
