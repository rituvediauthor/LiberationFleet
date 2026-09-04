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
    public void FindMiddlemen_WhenNoDirectOverlap_ReturnsSharedIntermediary()
    {
        var members = CreateMemberPlatforms();
        var service = CreateService();

        service.FindMiddlemen(1, 2, members).Should().BeEquivalentTo([3]);
    }

    [Fact]
    public void FindMiddlemen_WhenCandidateLacksIntermediaryRole_ReturnsEmpty()
    {
        var members = new List<CrewMemberPlatforms>
        {
            new() { UserId = 1, Username = "giver", PlatformIds = [1] },
            new() { UserId = 2, Username = "recipient", PlatformIds = [2] },
            new() { UserId = 3, Username = "middle", PlatformIds = [1, 2], IsIntermediary = false }
        };
        var service = CreateService();

        service.FindMiddlemen(1, 2, members).Should().BeEmpty();
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
    public async Task GetReceptionOrderAsync_PopulatesIntermediaryOptionsWhenPlatformsDoNotOverlap()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        var carolMembership = await fixture.Context.CrewMemberships.SingleAsync(m => m.UserId == fixture.Carol.Id);
        carolMembership.IsIntermediary = true;
        await fixture.Context.SaveChangesAsync();

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
        // Without an elected Intermediary, unmatched platforms are unavailable.
        nextAid.PlatformDisplayKind.Should().Be(NextAidPlatformDisplayKind.Unavailable);
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
            var service = HandlerTestFixture.CreateMutualAidService(context);

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
            c.CrewId == fixture.Crew.Id && c.UserId == fixture.Bob.Id && c.SeasonStartDate == fixture.SeasonStart);
        cycle.CycleReceived.Should().Be(125m);
        cycle.TotalReceptionAmount.Should().Be(125m);
        cycle.HasCycleStarted.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyGiftReceptionAsync_CustomGift_CreditsTotalWithoutStartingInactiveCycle()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        // Ensure Bob's cycle is not the active frontmost cycle.
        var bobCycle = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Bob.Id && c.SeasonStartDate == fixture.SeasonStart);
        bobCycle.HasCycleStarted = false;
        bobCycle.ReceptionOrderPosition = 10;
        await fixture.Context.SaveChangesAsync();
        await fixture.Service.GetReceptionOrderAsync(fixture.Alice.Id, cancellationToken: CancellationToken.None);

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

        var cycle = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Bob.Id && c.SeasonStartDate == fixture.SeasonStart);
        cycle.CycleReceived.Should().Be(0m);
        cycle.TotalReceptionAmount.Should().Be(50m);
        cycle.HasCycleStarted.Should().BeFalse();
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
    public async Task GetSeasonStatusAsync_StartsSeasonWhenThreeMembersAlreadyReady()
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

        var service = HandlerTestFixture.CreateMutualAidService(context);

        var status = await service.GetSeasonStatusAsync(users[0].Id, CancellationToken.None);

        status.SeasonStarted.Should().BeTrue();
        status.UserInSeason.Should().BeTrue();
        status.ReadyCount.Should().Be(3);

        var reloadedCrew = await context.Crews.SingleAsync(c => c.Id == crew.Id);
        reloadedCrew.SeasonStarted.Should().BeTrue();
        (await context.SeasonCycles.CountAsync(c => c.CrewId == crew.Id)).Should().Be(9);
        reloadedCrew.NextSeasonStartDate.Should().NotBeNull();
        reloadedCrew.FollowingSeasonStartDate.Should().NotBeNull();
        (await context.SeasonCycles.CountAsync(c =>
            c.CrewId == crew.Id && c.SeasonStartDate == reloadedCrew.CurrentSeasonStartDate)).Should().Be(3);
        (await context.SeasonCycles.CountAsync(c =>
            c.CrewId == crew.Id && c.SeasonStartDate == reloadedCrew.NextSeasonStartDate)).Should().Be(3);
        (await context.SeasonCycles.CountAsync(c =>
            c.CrewId == crew.Id && c.SeasonStartDate == reloadedCrew.FollowingSeasonStartDate)).Should().Be(3);
        (await context.SeasonCycles.CountAsync(c =>
            c.CrewId == crew.Id
            && c.SeasonStartDate == reloadedCrew.NextSeasonStartDate
            && c.CapIsProvisional)).Should().Be(3);
    }

    [Fact]
    public async Task MarkSeasonReadyAsync_StartsSeasonWhenThirdMemberMarksReady()
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
        for (var i = 0; i < users.Count; i++)
        {
            context.CrewMemberships.Add(new CrewMembership
            {
                UserId = users[i].Id,
                CrewId = crew.Id,
                EstimatedMonthlyContribution = 100m,
                IsSeasonReady = i < 2,
                JoinedAt = DateTime.UtcNow
            });
            context.UserPaymentPlatforms.Add(new UserPaymentPlatform
            {
                UserId = users[i].Id,
                CrewPaymentPlatformId = platforms["PayPal"].Id,
                Handle = $"@{users[i].Username}"
            });
        }
        await context.SaveChangesAsync();

        var service = HandlerTestFixture.CreateMutualAidService(context);

        var result = await service.MarkSeasonReadyAsync(users[2].Id, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SeasonStarted.Should().BeTrue();
        result.Status.Should().NotBeNull();
        result.Status!.UserInSeason.Should().BeTrue();

        var reloadedCrew = await context.Crews.SingleAsync(c => c.Id == crew.Id);
        reloadedCrew.SeasonStarted.Should().BeTrue();
        (await context.SeasonCycles.CountAsync(c => c.CrewId == crew.Id)).Should().Be(9);
        reloadedCrew.NextSeasonStartDate.Should().NotBeNull();
        reloadedCrew.FollowingSeasonStartDate.Should().NotBeNull();
        (await context.SeasonCycles.CountAsync(c =>
            c.CrewId == crew.Id && c.SeasonStartDate == reloadedCrew.CurrentSeasonStartDate)).Should().Be(3);
        (await context.SeasonCycles.CountAsync(c =>
            c.CrewId == crew.Id && c.SeasonStartDate == reloadedCrew.NextSeasonStartDate)).Should().Be(3);
        (await context.SeasonCycles.CountAsync(c =>
            c.CrewId == crew.Id && c.SeasonStartDate == reloadedCrew.FollowingSeasonStartDate)).Should().Be(3);
        (await context.SeasonCycles.CountAsync(c =>
            c.CrewId == crew.Id
            && c.SeasonStartDate == reloadedCrew.NextSeasonStartDate
            && c.CapIsProvisional)).Should().Be(3);
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

        var service = HandlerTestFixture.CreateMutualAidService(context);

        var result = await service.MarkSeasonReadyAsync(users[2].Id, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SeasonStarted.Should().BeTrue();

        var reloadedCrew = await context.Crews.SingleAsync(c => c.Id == crew.Id);
        reloadedCrew.SeasonStarted.Should().BeTrue();
        (await context.SeasonCycles.CountAsync(c => c.CrewId == crew.Id)).Should().Be(9);
        reloadedCrew.NextSeasonStartDate.Should().NotBeNull();
        reloadedCrew.FollowingSeasonStartDate.Should().NotBeNull();
        (await context.SeasonCycles.CountAsync(c =>
            c.CrewId == crew.Id && c.SeasonStartDate == reloadedCrew.CurrentSeasonStartDate)).Should().Be(3);
        (await context.SeasonCycles.CountAsync(c =>
            c.CrewId == crew.Id && c.SeasonStartDate == reloadedCrew.NextSeasonStartDate)).Should().Be(3);
        (await context.SeasonCycles.CountAsync(c =>
            c.CrewId == crew.Id && c.SeasonStartDate == reloadedCrew.FollowingSeasonStartDate)).Should().Be(3);
        (await context.SeasonCycles.CountAsync(c =>
            c.CrewId == crew.Id
            && c.SeasonStartDate == reloadedCrew.NextSeasonStartDate
            && c.CapIsProvisional)).Should().Be(3);
    }

    [Fact]
    public async Task GetPreviousSeasonStartDateAsync_WhenNoPriorSeason_ReturnsNull()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var repository = new MutualAidRepository(fixture.Context);

        var previous = await repository.GetPreviousSeasonStartDateAsync(
            fixture.Crew.Id,
            fixture.Crew.CurrentSeasonStartDate!.Value,
            CancellationToken.None);

        previous.Should().BeNull();
    }

    [Fact]
    public async Task GetPreviousSeasonStartDateAsync_WhenPriorSeasonExists_ReturnsMostRecentPriorStart()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var priorStart = fixture.SeasonStart.AddMonths(-3);
        fixture.Context.SeasonCycles.Add(new SeasonCycle
        {
            CrewId = fixture.Crew.Id,
            UserId = fixture.Alice.Id,
            SeasonStartDate = priorStart,
            CycleCapAtStart = 600m,
            CycleCompleted = true,
            ReceptionOrderPosition = 0
        });
        await fixture.Context.SaveChangesAsync();
        var repository = new MutualAidRepository(fixture.Context);

        var previous = await repository.GetPreviousSeasonStartDateAsync(
            fixture.Crew.Id,
            fixture.Crew.CurrentSeasonStartDate!.Value,
            CancellationToken.None);

        previous.Should().Be(priorStart);
    }

    [Fact]
    public async Task GetCrewMonthlyGivingCapacity_ExcludesLibraryOfThingsAndUsesJoinMonthRules()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var now = DateTime.UtcNow;
        var joinMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        foreach (var membership in fixture.Context.CrewMemberships)
        {
            membership.GivingSeasonJoinedAt = joinMonthStart.AddDays(1);
            membership.EstimatedMonthlyContribution = 90m;
        }

        var lotPlatform = new CrewPaymentPlatform
        {
            CrewId = fixture.Crew.Id,
            Name = "Library of Things",
            IsLibraryOfThings = true
        };
        fixture.Context.CrewPaymentPlatforms.Add(lotPlatform);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.Gifts.AddRange(
            new Gift
            {
                CrewId = fixture.Crew.Id,
                GiverUserId = fixture.Alice.Id,
                RecipientUserId = fixture.Bob.Id,
                Type = GiftType.Direct,
                Amount = 60m,
                CrewPaymentPlatformId = fixture.Platforms["PayPal"].Id,
                CountsTowardContribution = true,
                CreatedAt = joinMonthStart.AddDays(2)
            },
            new Gift
            {
                CrewId = fixture.Crew.Id,
                GiverUserId = fixture.Alice.Id,
                RecipientUserId = fixture.Bob.Id,
                Type = GiftType.Direct,
                Amount = 500m,
                CrewPaymentPlatformId = lotPlatform.Id,
                CrewPaymentPlatform = lotPlatform,
                CountsTowardContribution = true,
                CreatedAt = joinMonthStart.AddDays(3)
            });
        await fixture.Context.SaveChangesAsync();

        var capacity = await fixture.Service.GetCrewMonthlyGivingCapacityAsync(fixture.Crew.Id, CancellationToken.None);

        var priorMonthStart = joinMonthStart.AddMonths(-1);
        var twoMonthsAgoStart = joinMonthStart.AddMonths(-2);
        decimal MonthValue(DateTime monthStart, decimal actual) =>
            MutualAidCalculationService.GetCalendarMonthContribution(
                actual,
                monthStart,
                joinMonthStart,
                90m);

        var aliceAverage = MutualAidCalculationService.AverageMonthlyGivingCapacity(
        [
            MonthValue(twoMonthsAgoStart, 0m),
            MonthValue(priorMonthStart, 0m),
            MonthValue(joinMonthStart, 60m)
        ]);
        var othersAverage = MutualAidCalculationService.AverageMonthlyGivingCapacity(
        [
            MonthValue(twoMonthsAgoStart, 0m),
            MonthValue(priorMonthStart, 0m),
            MonthValue(joinMonthStart, 0m)
        ]);

        capacity.Should().Be(aliceAverage + othersAverage + othersAverage);
    }

    [Fact]
    public async Task GetCrewMonthlyGivingCapacity_WhenJoinedWithFewerThan15DaysLeft_RoundsJoinToNextMonth()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var now = DateTime.UtcNow;
        var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        var lateJoin = new DateTime(now.Year, now.Month, daysInMonth, 12, 0, 0, DateTimeKind.Utc);
        foreach (var membership in fixture.Context.CrewMemberships)
        {
            membership.GivingSeasonJoinedAt = lateJoin;
            membership.EstimatedMonthlyContribution = 90m;
        }

        await fixture.Context.SaveChangesAsync();

        var capacity = await fixture.Service.GetCrewMonthlyGivingCapacityAsync(fixture.Crew.Id, CancellationToken.None);

        capacity.Should().Be(270m);
    }

    [Fact]
    public async Task GetReceptionOrderAsync_CreatesCurrentMonthSurvivalThresholdsWhenMissing()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        fixture.Carol.NeedsSurvivalAid = true;
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.GetReceptionOrderAsync(fixture.Alice.Id, cancellationToken: CancellationToken.None);

        var now = DateTime.UtcNow;
        (await fixture.Context.MonthlySurvivalThresholds.CountAsync(t =>
            t.CrewId == fixture.Crew.Id
            && t.UserId == fixture.Carol.Id
            && t.Year == now.Year
            && t.Month == now.Month)).Should().Be(1);
    }

    [Fact]
    public async Task OnInNeedOfAidChanged_WhenNoLongerInNeed_CompletesAndDeactivatesCurrentCycle()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var cycle = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Bob.Id && c.SeasonStartDate == fixture.SeasonStart);
        cycle.HasCycleStarted = true;
        await fixture.Context.SaveChangesAsync();

        fixture.Bob.InNeedOfAid = false;
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.OnInNeedOfAidChangedAsync(fixture.Bob.Id, isInNeedOfAid: false, CancellationToken.None);

        var reloaded = await fixture.Context.SeasonCycles.SingleAsync(c => c.Id == cycle.Id);
        reloaded.CycleCompleted.Should().BeTrue();
        reloaded.HasCycleStarted.Should().BeFalse();
    }

    [Fact]
    public async Task OnInNeedOfAidChanged_WhenBackInNeedAndUnderCap_ReopensCycle()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var cycle = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Bob.Id && c.SeasonStartDate == fixture.SeasonStart);
        cycle.CycleCompleted = true;
        cycle.CycleCompletedAt = DateTime.UtcNow;
        cycle.HasCycleStarted = false;
        cycle.CycleReceived = 100m;
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.OnInNeedOfAidChangedAsync(fixture.Bob.Id, isInNeedOfAid: true, CancellationToken.None);

        var reloaded = await fixture.Context.SeasonCycles.SingleAsync(c => c.Id == cycle.Id);
        reloaded.CycleCompleted.Should().BeFalse();
        reloaded.CycleReceived.Should().Be(100m);
    }

    [Fact]
    public async Task OnInNeedOfAidChanged_WhenBackInNeedAndAtCap_DoesNotReopenCycle()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var cycle = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Bob.Id && c.SeasonStartDate == fixture.SeasonStart);
        cycle.CycleCompleted = true;
        cycle.CycleCompletedAt = DateTime.UtcNow;
        cycle.HasCycleStarted = false;
        cycle.CycleReceived = 600m;
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.OnInNeedOfAidChangedAsync(fixture.Bob.Id, isInNeedOfAid: true, CancellationToken.None);

        var reloaded = await fixture.Context.SeasonCycles.SingleAsync(c => c.Id == cycle.Id);
        reloaded.CycleCompleted.Should().BeTrue();
        reloaded.CycleReceived.Should().Be(600m);
    }

    [Fact]
    public async Task OnInNeedOfAidChanged_WhenNoCurrentCycleExists_CreatesOne()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var existing = await fixture.Context.SeasonCycles
            .Where(c => c.UserId == fixture.Carol.Id && c.SeasonStartDate == fixture.SeasonStart)
            .ToListAsync();
        fixture.Context.SeasonCycles.RemoveRange(existing);
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.OnInNeedOfAidChangedAsync(fixture.Carol.Id, isInNeedOfAid: true, CancellationToken.None);

        (await fixture.Context.SeasonCycles.CountAsync(c =>
            c.UserId == fixture.Carol.Id
            && c.SeasonStartDate == fixture.SeasonStart
            && !c.CycleCompleted)).Should().Be(1);
    }

    [Fact]
    public async Task OnInNeedOfAidChanged_WhenOptOutWithEmergencySegment_ForgivesSegmentAndRestoresPrimary()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var primary = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Bob.Id
            && c.SeasonStartDate == fixture.SeasonStart
            && !c.EmergencySplitOfferId.HasValue
            && !c.EmergencyRequestId.HasValue);
        primary.UsesSegmentCap = true;
        primary.CycleCapAtStart = 50m;
        primary.HasCycleStarted = true;
        var segment = new SeasonCycle
        {
            CrewId = fixture.Crew.Id,
            UserId = fixture.Bob.Id,
            SeasonStartDate = fixture.SeasonStart,
            CycleCapAtStart = 50m,
            UsesSegmentCap = true,
            EmergencySplitOfferId = 1,
            CycleReceived = 25m,
            CycleCompleted = false,
            ReceptionOrderPosition = primary.ReceptionOrderPosition,
            PriorityScoreAtSeasonStart = primary.PriorityScoreAtSeasonStart
        };
        fixture.Context.SeasonCycles.Add(segment);
        await fixture.Context.SaveChangesAsync();

        fixture.Bob.InNeedOfAid = false;
        await fixture.Context.SaveChangesAsync();
        await fixture.Service.OnInNeedOfAidChangedAsync(fixture.Bob.Id, isInNeedOfAid: false, CancellationToken.None);

        var reloadedPrimary = await fixture.Context.SeasonCycles.SingleAsync(c => c.Id == primary.Id);
        var reloadedSegment = await fixture.Context.SeasonCycles.SingleAsync(c => c.Id == segment.Id);
        reloadedPrimary.CycleCompleted.Should().BeTrue();
        reloadedSegment.CycleCompleted.Should().BeTrue();
        reloadedSegment.CycleReceived.Should().Be(25m);
        // Forgiven remainder ($25) restored onto primary ($50 → $75).
        reloadedPrimary.CycleCapAtStart.Should().Be(75m);

        fixture.Bob.InNeedOfAid = true;
        await fixture.Context.SaveChangesAsync();
        await fixture.Service.OnInNeedOfAidChangedAsync(fixture.Bob.Id, isInNeedOfAid: true, CancellationToken.None);

        reloadedPrimary = await fixture.Context.SeasonCycles.SingleAsync(c => c.Id == primary.Id);
        reloadedSegment = await fixture.Context.SeasonCycles.SingleAsync(c => c.Id == segment.Id);
        reloadedPrimary.CycleCompleted.Should().BeFalse();
        // Forgiven segment stays complete on re-opt-in.
        reloadedSegment.CycleCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task SaveSeasonSetupAsync_AllowsZeroEstimatedContribution()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        var result = await fixture.Service.SaveSeasonSetupAsync(fixture.Alice.Id, 0m, CancellationToken.None);

        result.Success.Should().BeTrue();
        var membership = await fixture.Context.CrewMemberships.SingleAsync(m =>
            m.UserId == fixture.Alice.Id && m.CrewId == fixture.Crew.Id);
        membership.EstimatedMonthlyContribution.Should().Be(0m);
    }

    [Fact]
    public async Task SaveSeasonSetupAsync_RejectsNegativeEstimatedContribution()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        var result = await fixture.Service.SaveSeasonSetupAsync(fixture.Alice.Id, -1m, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("negative");
    }

    [Fact]
    public async Task MarkSeasonReadyAsync_AllowsZeroEstimatedContribution()
    {
        var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedPaymentPlatformsAsync(context);

        var user = new User
        {
            Username = "zero",
            Email = "zero@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var crew = HandlerTestFixture.CreateCrew(createdByUserId: user.Id);
        crew.SeasonStarted = false;
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var platforms = await TestDbContextFactory.SeedCrewPaymentPlatformsAsync(context, crew.Id);
        context.CrewMemberships.Add(new CrewMembership
        {
            UserId = user.Id,
            CrewId = crew.Id,
            EstimatedMonthlyContribution = 0m,
            JoinedAt = DateTime.UtcNow
        });
        context.UserPaymentPlatforms.Add(new UserPaymentPlatform
        {
            UserId = user.Id,
            CrewPaymentPlatformId = platforms["PayPal"].Id,
            Handle = "@zero"
        });
        await context.SaveChangesAsync();

        var service = HandlerTestFixture.CreateMutualAidService(context);
        var result = await service.MarkSeasonReadyAsync(user.Id, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status!.UserSeasonReady.Should().BeTrue();
    }

    [Fact]
    public async Task IsFinancialMemberAsync_WhenNoRoleAndNoRecentGifts_ReturnsFalse()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var membership = await fixture.Context.CrewMemberships.SingleAsync(m =>
            m.UserId == fixture.Alice.Id && m.CrewId == fixture.Crew.Id);
        membership.IsHonoraryMember = false;
        membership.GivingSeasonJoinedAt = DateTime.UtcNow.AddMonths(-4);
        membership.EstimatedMonthlyContribution = 90m;
        await fixture.Context.SaveChangesAsync();

        var isMember = await fixture.Service.IsFinancialMemberAsync(
            fixture.Alice.Id,
            fixture.Crew.Id,
            membership,
            CancellationToken.None);

        isMember.Should().BeFalse();
    }

    [Fact]
    public async Task IsFinancialMemberAsync_WhenRoleHeld_DoesNotExpire()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var membership = await fixture.Context.CrewMemberships.SingleAsync(m =>
            m.UserId == fixture.Alice.Id && m.CrewId == fixture.Crew.Id);
        membership.IsHonoraryMember = false;
        membership.IsAccountant = true;
        membership.GivingSeasonJoinedAt = DateTime.UtcNow.AddMonths(-4);
        membership.EstimatedMonthlyContribution = 0m;
        await fixture.Context.SaveChangesAsync();

        var isMember = await fixture.Service.IsFinancialMemberAsync(
            fixture.Alice.Id,
            fixture.Crew.Id,
            membership,
            CancellationToken.None);

        isMember.Should().BeTrue();
    }

    [Fact]
    public async Task IsFinancialMemberAsync_WhenLibraryOfThingsGiftsMeetAverage_ReturnsTrue()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var membership = await fixture.Context.CrewMemberships.SingleAsync(m =>
            m.UserId == fixture.Alice.Id && m.CrewId == fixture.Crew.Id);
        membership.IsHonoraryMember = false;
        membership.GivingSeasonJoinedAt = DateTime.UtcNow.AddMonths(-4);
        membership.EstimatedMonthlyContribution = 0m;
        var lotPlatform = new CrewPaymentPlatform
        {
            CrewId = fixture.Crew.Id,
            Name = "Library of Things",
            IsLibraryOfThings = true
        };
        fixture.Context.CrewPaymentPlatforms.Add(lotPlatform);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.Gifts.Add(new Gift
        {
            CrewId = fixture.Crew.Id,
            GiverUserId = fixture.Alice.Id,
            RecipientUserId = fixture.Bob.Id,
            Type = GiftType.Direct,
            Amount = 30m,
            CrewPaymentPlatformId = lotPlatform.Id,
            CrewPaymentPlatform = lotPlatform,
            CountsTowardContribution = true,
            CreatedAt = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();

        var isMember = await fixture.Service.IsFinancialMemberAsync(
            fixture.Alice.Id,
            fixture.Crew.Id,
            membership,
            CancellationToken.None);

        isMember.Should().BeTrue();
    }

    [Fact]
    public async Task IsFinancialMemberAsync_WhenAverageBelowCrewFloor_ReturnsFalse()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        fixture.Crew.FinancialMembershipContributionFloor = 15m;
        var membership = await fixture.Context.CrewMemberships.SingleAsync(m =>
            m.UserId == fixture.Alice.Id && m.CrewId == fixture.Crew.Id);
        membership.IsHonoraryMember = false;
        membership.GivingSeasonJoinedAt = DateTime.UtcNow.AddMonths(-4);
        membership.EstimatedMonthlyContribution = 0m;
        await fixture.Context.SaveChangesAsync();

        fixture.Context.Gifts.Add(new Gift
        {
            CrewId = fixture.Crew.Id,
            GiverUserId = fixture.Alice.Id,
            RecipientUserId = fixture.Bob.Id,
            Type = GiftType.Direct,
            Amount = 30m,
            CountsTowardContribution = true,
            CreatedAt = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();

        var isMember = await fixture.Service.IsFinancialMemberAsync(
            fixture.Alice.Id,
            fixture.Crew.Id,
            membership,
            CancellationToken.None);

        isMember.Should().BeFalse();
    }

    [Fact]
    public async Task IsFinancialMemberAsync_WhenAverageMeetsCrewFloor_ReturnsTrue()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        fixture.Crew.FinancialMembershipContributionFloor = 10m;
        var membership = await fixture.Context.CrewMemberships.SingleAsync(m =>
            m.UserId == fixture.Alice.Id && m.CrewId == fixture.Crew.Id);
        membership.IsHonoraryMember = false;
        membership.GivingSeasonJoinedAt = DateTime.UtcNow.AddMonths(-4);
        membership.EstimatedMonthlyContribution = 0m;
        await fixture.Context.SaveChangesAsync();

        fixture.Context.Gifts.Add(new Gift
        {
            CrewId = fixture.Crew.Id,
            GiverUserId = fixture.Alice.Id,
            RecipientUserId = fixture.Bob.Id,
            Type = GiftType.Direct,
            Amount = 30m,
            CountsTowardContribution = true,
            CreatedAt = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();

        var isMember = await fixture.Service.IsFinancialMemberAsync(
            fixture.Alice.Id,
            fixture.Crew.Id,
            membership,
            CancellationToken.None);

        isMember.Should().BeTrue();
    }

    [Fact]
    public async Task GetReceptionOrderAsync_ShowsCatchUpOnlyAfterMonthlySnapshot()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var bobCycle = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Bob.Id && c.SeasonStartDate == fixture.SeasonStart);
        bobCycle.CycleCompleted = true;
        bobCycle.CycleCompletedAt = DateTime.UtcNow;
        bobCycle.CycleReceived = 100m;
        await fixture.Context.SaveChangesAsync();

        var beforeSnapshot = await fixture.Service.GetReceptionOrderAsync(
            fixture.Alice.Id,
            forRecordGift: true,
            cancellationToken: CancellationToken.None);
        beforeSnapshot.Should().Contain(e => e.UserId == fixture.Bob.Id && e.EntryType == "catchUp");

        var carolCycle = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Carol.Id && c.SeasonStartDate == fixture.SeasonStart);
        carolCycle.CycleCompleted = true;
        carolCycle.CycleCompletedAt = DateTime.UtcNow;
        carolCycle.CycleReceived = 100m;
        await fixture.Context.SaveChangesAsync();

        var sameMonth = await fixture.Service.GetReceptionOrderAsync(
            fixture.Alice.Id,
            forRecordGift: true,
            cancellationToken: CancellationToken.None);
        sameMonth.Should().NotContain(e => e.UserId == fixture.Carol.Id && e.EntryType == "catchUp");

        fixture.Crew.CatchUpSnapshotMonth = 0;
        fixture.Crew.CatchUpSnapshotYear = 0;
        await fixture.Context.SaveChangesAsync();

        var nextEval = await fixture.Service.GetReceptionOrderAsync(
            fixture.Alice.Id,
            forRecordGift: true,
            cancellationToken: CancellationToken.None);
        nextEval.Should().Contain(e => e.UserId == fixture.Carol.Id && e.EntryType == "catchUp");
    }

    [Fact]
    public async Task GetPrimarySeasonCycleAsync_WhenSegmentExists_ReturnsPrimary()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var primary = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Bob.Id
            && c.SeasonStartDate == fixture.SeasonStart
            && c.EmergencyRequestId == null
            && c.EmergencySplitOfferId == null);
        // Insert segment with lower Id order ambiguity via FirstOrDefault without filter.
        fixture.Context.SeasonCycles.Add(new SeasonCycle
        {
            CrewId = fixture.Crew.Id,
            UserId = fixture.Bob.Id,
            SeasonStartDate = fixture.SeasonStart,
            CycleCapAtStart = 25m,
            EmergencySplitOfferId = 7,
            CycleReceived = 5m,
            ReceptionOrderPosition = primary.ReceptionOrderPosition - 1,
            PriorityScoreAtSeasonStart = primary.PriorityScoreAtSeasonStart
        });
        await fixture.Context.SaveChangesAsync();

        var repo = new MutualAidRepository(fixture.Context);
        var found = await repo.GetPrimarySeasonCycleAsync(
            fixture.Crew.Id,
            fixture.Bob.Id,
            fixture.SeasonStart,
            CancellationToken.None);

        found.Should().NotBeNull();
        found!.Id.Should().Be(primary.Id);
        found.EmergencySplitOfferId.Should().BeNull();
    }

    [Fact]
    public async Task MergePlaceholderIdentity_TransfersPrimaryAndSegments()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var placeholder = new User
        {
            Username = "placeholder",
            Email = "placeholder@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsUnclaimedPlaceholder = true,
            InNeedOfAid = true
        };
        fixture.Context.Users.Add(placeholder);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.CrewMemberships.Add(new CrewMembership
        {
            UserId = placeholder.Id,
            CrewId = fixture.Crew.Id,
            IsBanned = false,
            JoinedAt = DateTime.UtcNow,
            IsPlaceholderMember = true,
            IsInSeason = true,
            IsSeasonReady = true,
            IsHonoraryMember = true,
            EstimatedMonthlyContribution = 50m
        });
        var placeholderPrimary = new SeasonCycle
        {
            CrewId = fixture.Crew.Id,
            UserId = placeholder.Id,
            SeasonStartDate = fixture.SeasonStart,
            CycleCapAtStart = 100m,
            CycleReceived = 20m,
            TotalReceptionAmount = 20m,
            ReceptionOrderPosition = 9,
            PriorityScoreAtSeasonStart = 50m
        };
        var placeholderSegment = new SeasonCycle
        {
            CrewId = fixture.Crew.Id,
            UserId = placeholder.Id,
            SeasonStartDate = fixture.SeasonStart,
            CycleCapAtStart = 30m,
            EmergencyRequestId = 42,
            CycleReceived = 10m,
            ReceptionOrderPosition = 8,
            PriorityScoreAtSeasonStart = 50m
        };
        fixture.Context.SeasonCycles.AddRange(placeholderPrimary, placeholderSegment);
        await fixture.Context.SaveChangesAsync();

        // Claimant already has a primary — merge amounts and reassign segment.
        var repo = new MutualAidRepository(fixture.Context);
        await repo.MergePlaceholderIdentityDataAsync(
            fixture.Crew.Id,
            placeholder.Id,
            fixture.Carol.Id,
            CancellationToken.None);
        await fixture.Context.SaveChangesAsync();

        (await fixture.Context.SeasonCycles.CountAsync(c => c.UserId == placeholder.Id)).Should().Be(0);
        var carolPrimary = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Carol.Id
            && c.SeasonStartDate == fixture.SeasonStart
            && c.EmergencyRequestId == null
            && c.EmergencySplitOfferId == null);
        carolPrimary.CycleReceived.Should().Be(20m);
        var transferredSegment = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.EmergencyRequestId == 42);
        transferredSegment.UserId.Should().Be(fixture.Carol.Id);
    }

    [Fact]
    public async Task SimulateNewSeasonAsync_UsesProductionRolloverWithoutDuplicateCurrentPrimaries()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var previousStart = fixture.Crew.CurrentSeasonStartDate!.Value;
        var nextStart = fixture.Crew.NextSeasonStartDate!.Value;

        var result = await fixture.Service.SimulateNewSeasonAsync(fixture.Alice.Id, CancellationToken.None);

        result.Success.Should().BeTrue();
        var crew = await fixture.Context.Crews.SingleAsync(c => c.Id == fixture.Crew.Id);
        crew.CurrentSeasonStartDate.Should().Be(nextStart);
        crew.CurrentSeasonStartDate.Should().NotBe(previousStart);

        var currentPrimaries = await fixture.Context.SeasonCycles
            .Where(c =>
                c.CrewId == fixture.Crew.Id
                && c.SeasonStartDate == crew.CurrentSeasonStartDate
                && c.EmergencyRequestId == null
                && c.EmergencySplitOfferId == null)
            .GroupBy(c => c.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();
        currentPrimaries.Should().OnlyContain(g => g.Count == 1);
    }

    [Fact]
    public async Task ResetSeasonAsync_ClearsCyclesThresholdsAndReadyState()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        await fixture.AddUnsatisfiedThresholdAsync(fixture.Bob, thresholdAmount: 25m);

        var result = await fixture.Service.ResetSeasonAsync(fixture.Alice.Id, CancellationToken.None);

        result.Success.Should().BeTrue();
        var crew = await fixture.Context.Crews.SingleAsync(c => c.Id == fixture.Crew.Id);
        crew.SeasonStarted.Should().BeFalse();
        crew.CurrentSeasonStartDate.Should().BeNull();
        crew.CatchUpSnapshotYear.Should().Be(0);
        (await fixture.Context.SeasonCycles.CountAsync(c => c.CrewId == fixture.Crew.Id)).Should().Be(0);
        (await fixture.Context.MonthlySurvivalThresholds.CountAsync(t => t.CrewId == fixture.Crew.Id)).Should().Be(0);
        (await fixture.Context.CrewMemberships.CountAsync(m => m.CrewId == fixture.Crew.Id && m.IsSeasonReady)).Should().Be(0);
        (await fixture.Context.CrewMemberships.CountAsync(m => m.CrewId == fixture.Crew.Id && m.IsInSeason)).Should().Be(0);
    }

    [Fact]
    public async Task MarkSeasonReady_WhenNonNeederJoinsFreshSeason_CreatesCompletedPrimary()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        await fixture.Service.ResetSeasonAsync(fixture.Alice.Id, CancellationToken.None);

        fixture.Carol.InNeedOfAid = false;
        await fixture.Context.SaveChangesAsync();

        foreach (var user in new[] { fixture.Alice, fixture.Bob, fixture.Carol })
        {
            var ready = await fixture.Service.MarkSeasonReadyAsync(user.Id, CancellationToken.None);
            ready.Success.Should().BeTrue();
        }

        var carolCycle = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Carol.Id
            && c.EmergencyRequestId == null
            && c.EmergencySplitOfferId == null
            && c.SeasonStartDate == fixture.Context.Crews.Single(crew => crew.Id == fixture.Crew.Id).CurrentSeasonStartDate);
        carolCycle.CycleCompleted.Should().BeTrue();
        carolCycle.HasCycleStarted.Should().BeFalse();

        var bobCycle = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Bob.Id
            && c.EmergencyRequestId == null
            && c.EmergencySplitOfferId == null
            && c.SeasonStartDate == carolCycle.SeasonStartDate);
        bobCycle.CycleCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetReceptionOrderAsync_ForRecordGift_IncludesNextSeasonCycleWhenCurrentSeasonHasOneLeft()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var currentCycles = await fixture.Context.SeasonCycles
            .Where(c => c.SeasonStartDate == fixture.SeasonStart)
            .ToListAsync();
        foreach (var cycle in currentCycles.Where(c => c.UserId != fixture.Bob.Id))
        {
            cycle.CycleCompleted = true;
            cycle.CycleCompletedAt = DateTime.UtcNow;
            cycle.CycleReceived = cycle.CycleCapAtStart > 0 ? cycle.CycleCapAtStart : 100m;
        }

        var bobCurrent = currentCycles.Single(c => c.UserId == fixture.Bob.Id);
        bobCurrent.HasCycleStarted = true;
        await fixture.Context.SaveChangesAsync();

        var nextSeasonStart = fixture.Crew.NextSeasonStartDate!.Value;
        var nextLeader = await fixture.Context.SeasonCycles
            .Where(c => c.SeasonStartDate == nextSeasonStart && !c.CycleCompleted)
            .OrderBy(c => c.ReceptionOrderPosition)
            .FirstAsync();

        var order = await fixture.Service.GetReceptionOrderAsync(
            fixture.Alice.Id,
            forRecordGift: true,
            cancellationToken: CancellationToken.None);

        var cycleEntries = order.Where(e => e.EntryType == "cycle").ToList();
        cycleEntries.Should().HaveCount(2);
        cycleEntries.Select(e => e.SeasonCycleId).Should().Contain(bobCurrent.Id);
        cycleEntries.Select(e => e.SeasonCycleId).Should().Contain(nextLeader.Id);
    }

    [Fact]
    public async Task ApplyGiftReceptionAsync_ScopedSurvivalGiftDoesNotBurnSiblingThreshold()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var older = await fixture.AddUnsatisfiedThresholdAsync(
            fixture.Bob,
            thresholdAmount: 40m,
            year: DateTime.UtcNow.Year,
            month: DateTime.UtcNow.Month == 1 ? 12 : DateTime.UtcNow.Month - 1);
        if (DateTime.UtcNow.Month == 1)
        {
            older.Year = DateTime.UtcNow.Year - 1;
            await fixture.Context.SaveChangesAsync();
        }

        var newer = await fixture.AddUnsatisfiedThresholdAsync(fixture.Bob, thresholdAmount: 40m);

        var gift = new Gift
        {
            CrewId = fixture.Crew.Id,
            GiverUserId = fixture.Alice.Id,
            RecipientUserId = fixture.Bob.Id,
            Type = GiftType.Direct,
            Amount = 25m,
            CrewPaymentPlatformId = fixture.Platforms["PayPal"].Id,
            CountsTowardReception = true,
            IsSurvivalThreshold = true,
            MonthlySurvivalThresholdId = newer.Id,
            CreatedAt = DateTime.UtcNow
        };
        fixture.Context.Gifts.Add(gift);
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.ApplyGiftReceptionAsync(gift, CancellationToken.None);

        var olderReloaded = await fixture.Context.MonthlySurvivalThresholds.SingleAsync(t => t.Id == older.Id);
        var newerReloaded = await fixture.Context.MonthlySurvivalThresholds.SingleAsync(t => t.Id == newer.Id);
        olderReloaded.ReceivedAmount.Should().Be(0m);
        newerReloaded.ReceivedAmount.Should().Be(25m);
    }

    private static IReadOnlyList<CrewMemberPlatforms> CreateMemberPlatforms() =>
    [
        new CrewMemberPlatforms { UserId = 1, Username = "giver", PlatformIds = [1] },
        new CrewMemberPlatforms { UserId = 2, Username = "recipient", PlatformIds = [2] },
        new CrewMemberPlatforms { UserId = 3, Username = "middle", PlatformIds = [1, 2], IsIntermediary = true }
    ];

    private static MutualAidService CreateService()
    {
        var context = TestDbContextFactory.Create();
        return HandlerTestFixture.CreateMutualAidService(context);
    }
}

public class ReceptionEntryTypeTests
{
    [Theory]
    [InlineData(ReceptionEntryType.SurvivalThreshold, "survivalThreshold")]
    [InlineData(ReceptionEntryType.Cycle, "cycle")]
    [InlineData(ReceptionEntryType.CatchUp, "catchUp")]
    [InlineData(ReceptionEntryType.Representative, "representative")]
    public void ToApiValue_ReturnsCamelCaseWireValues(ReceptionEntryType entryType, string expected)
    {
        entryType.ToApiValue().Should().Be(expected);
        ReceptionEntryTypeExtensions.TryParseApiValue(expected, out var parsed).Should().BeTrue();
        parsed.Should().Be(entryType);
    }
}
