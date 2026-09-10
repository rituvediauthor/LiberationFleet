using LiberationFleet.Server.Application.Features.EmergencyRequests;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Features.EmergencyRequests;

public class EmergencySplitServiceTests
{
    [Fact]
    public async Task ApplySplit_FromLockedRunnerUp_InsertsEmergencyPackageIntoVisibleOrder()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        // Order: Bob(0), Alice(1), Carol(2). Locked = Bob + Alice.
        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 50m);
        var splitService = CreateSplitService(fx);

        var result = await splitService.ApplySplitAsync(request, fx.Alice.Id, 50m, CancellationToken.None);
        await fx.Context.SaveChangesAsync();

        result.Success.Should().BeTrue();
        request.AmountSplitCommitted.Should().Be(50m);
        request.AmountReceived.Should().Be(0m);
        request.Status.Should().Be(EmergencyRequestStatus.Open);

        var cycles = await fx.Context.SeasonCycles
            .Where(c => c.SeasonStartDate == fx.SeasonStart && !c.CycleCompleted)
            .OrderBy(c => c.ReceptionOrderPosition)
            .ToListAsync();

        // Bob 100, Carol emergency 50, Alice remaining 50, Carol remaining 50, Alice payback 50
        // Actually: emergency before Alice, payback before Carol primary.
        // Bob(100), Carol(50 emergency), Alice(50), Alice(50 payback), Carol(50)
        cycles.Select(c => (c.UserId, c.CycleCapAtStart, c.EmergencyRequestId.HasValue, c.EmergencySplitOfferId.HasValue))
            .Should().BeEquivalentTo(
            [
                (fx.Bob.Id, 100m, false, false),
                (fx.Carol.Id, 50m, true, false),
                (fx.Alice.Id, 50m, false, false),
                (fx.Alice.Id, 50m, false, true),
                (fx.Carol.Id, 50m, false, false)
            ],
            opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task ApplySplit_FromStartedActiveLeader_FiftyDollarSplit_LinksPaybackToOffer()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var bobPrimary = await fx.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fx.Bob.Id && c.SeasonStartDate == fx.SeasonStart);
        bobPrimary.HasCycleStarted = true;
        await fx.Context.SaveChangesAsync();

        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 50m);
        var splitService = CreateSplitService(fx);

        var result = await splitService.ApplySplitAsync(request, fx.Bob.Id, 50m, CancellationToken.None);
        await fx.Context.SaveChangesAsync();

        result.Success.Should().BeTrue(because: result.Message);
        await AssertSplitLinkedAsync(fx, request, fx.Bob.Id, 50m, EmergencyOffererQueueRole.ActiveCycle);
    }

    [Fact]
    public async Task ApplySplit_FromStartedActiveLeader_FiftyDollarSplit_PersistsOnSqlite()
    {
        // Relational FKs catch the circular offer↔payback graph that InMemory allows.
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m, useSqlite: true);
        var bobPrimary = await fx.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fx.Bob.Id && c.SeasonStartDate == fx.SeasonStart
            && c.EmergencyRequestId == null && c.EmergencySplitOfferId == null);
        bobPrimary.HasCycleStarted = true;
        await fx.Context.SaveChangesAsync();

        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 50m);
        var splitService = CreateSplitService(fx);

        var result = await splitService.ApplySplitAsync(request, fx.Bob.Id, 50m, CancellationToken.None);
        await fx.Context.SaveChangesAsync();

        result.Success.Should().BeTrue(because: result.Message);
        await AssertSplitLinkedAsync(fx, request, fx.Bob.Id, 50m, EmergencyOffererQueueRole.ActiveCycle);
    }

    [Fact]
    public async Task ApplySplit_FromActiveLeader_CanInsertEmergencyBeforeActiveCycle()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 50m);
        var splitService = CreateSplitService(fx);

        var result = await splitService.ApplySplitAsync(request, fx.Bob.Id, 25m, CancellationToken.None);
        await fx.Context.SaveChangesAsync();

        result.Success.Should().BeTrue();

        var cycles = await fx.Context.SeasonCycles
            .Where(c => c.SeasonStartDate == fx.SeasonStart && !c.CycleCompleted)
            .OrderBy(c => c.ReceptionOrderPosition)
            .ToListAsync();

        // Carol emergency before Bob remaining; Bob payback before Carol remaining.
        cycles[0].UserId.Should().Be(fx.Carol.Id);
        cycles[0].EmergencyRequestId.Should().NotBeNull();
        cycles[0].CycleCapAtStart.Should().Be(25m);
        cycles[1].UserId.Should().Be(fx.Bob.Id);
        cycles[1].CycleCapAtStart.Should().Be(75m);
        cycles[1].EmergencyRequestId.Should().BeNull();
    }

    [Fact]
    public async Task ApplySplit_WhenRequesterCurrentCycleCompleted_UsesNextSeasonPrimary()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);

        var carolCurrent = await fx.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fx.Carol.Id && c.SeasonStartDate == fx.SeasonStart);
        carolCurrent.CycleReceived = 100m;
        carolCurrent.CycleCompleted = true;
        carolCurrent.CycleCompletedAt = DateTime.UtcNow;
        carolCurrent.UsesSegmentCap = true;
        carolCurrent.CycleCapAtStart = 100m;
        await fx.Context.SaveChangesAsync();

        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 50m);
        var splitService = CreateSplitService(fx);

        var result = await splitService.ApplySplitAsync(request, fx.Alice.Id, 50m, CancellationToken.None);
        await fx.Context.SaveChangesAsync();

        result.Success.Should().BeTrue(because: result.Message);

        var currentCycles = await fx.Context.SeasonCycles
            .Where(c => c.SeasonStartDate == fx.SeasonStart && !c.CycleCompleted)
            .OrderBy(c => c.ReceptionOrderPosition)
            .ToListAsync();

        currentCycles.Should().Contain(c =>
            c.UserId == fx.Carol.Id && c.EmergencyRequestId == request.Id && c.CycleCapAtStart == 50m);

        var nextStart = fx.Crew.NextSeasonStartDate!.Value;
        var nextCycles = await fx.Context.SeasonCycles
            .Where(c => c.SeasonStartDate == nextStart && !c.CycleCompleted)
            .OrderBy(c => c.ReceptionOrderPosition)
            .ToListAsync();

        nextCycles.Should().Contain(c =>
            c.UserId == fx.Alice.Id && c.EmergencySplitOfferId.HasValue && c.CycleCapAtStart == 50m);
        var carolNextPrimary = nextCycles.Single(c =>
            c.UserId == fx.Carol.Id
            && !c.EmergencyRequestId.HasValue
            && !c.EmergencySplitOfferId.HasValue);
        carolNextPrimary.CapIsProvisional.Should().BeTrue();
        carolNextPrimary.SplitReservedAmount.Should().Be(50m);
        carolNextPrimary.CycleCompleted.Should().BeFalse();

        // Still has following-season incomplete primary so ≥2 incompletes remain.
        var followingStart = fx.Crew.FollowingSeasonStartDate!.Value;
        (await fx.Context.SeasonCycles.CountAsync(c =>
            c.UserId == fx.Carol.Id
            && c.SeasonStartDate == followingStart
            && !c.CycleCompleted
            && c.EmergencyRequestId == null
            && c.EmergencySplitOfferId == null)).Should().Be(1);
    }

    [Fact]
    public async Task ApplySplit_TwoOffers_KeepsPaybacksOrderedBySplitSequence()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 50m);
        var splitService = CreateSplitService(fx);

        (await splitService.ApplySplitAsync(request, fx.Bob.Id, 25m, CancellationToken.None)).Success.Should().BeTrue();
        await fx.Context.SaveChangesAsync();
        (await splitService.ApplySplitAsync(request, fx.Alice.Id, 25m, CancellationToken.None)).Success.Should().BeTrue();
        await fx.Context.SaveChangesAsync();

        var cycles = await fx.Context.SeasonCycles
            .Where(c => c.SeasonStartDate == fx.SeasonStart && !c.CycleCompleted)
            .OrderBy(c => c.ReceptionOrderPosition)
            .ToListAsync();

        var paybacks = cycles.Where(c => c.EmergencySplitOfferId.HasValue).ToList();
        paybacks.Should().HaveCount(2);
        paybacks[0].UserId.Should().Be(fx.Bob.Id);
        paybacks[1].UserId.Should().Be(fx.Alice.Id);
        cycles.Last(c => !c.EmergencyRequestId.HasValue && !c.EmergencySplitOfferId.HasValue && c.UserId == fx.Carol.Id)
            .CycleCapAtStart.Should().Be(50m);
    }

    [Fact]
    public async Task ApplySplit_WhenOffererWasNotAheadAtRequestTime_Fails()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        // Only Bob was ahead of Carol when the request was created — Alice may not split.
        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 50m, fx.Bob);
        var splitService = CreateSplitService(fx);

        var result = await splitService.ApplySplitAsync(request, fx.Alice.Id, 25m, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("current and next cycle");
    }

    [Fact]
    public async Task ApplySplit_RejectsAmountAboveLiveRemainingCapacity()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var alicePrimary = await fx.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fx.Alice.Id && c.SeasonStartDate == fx.SeasonStart);
        alicePrimary.CycleReceived = 80m;
        await fx.Context.SaveChangesAsync();

        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 50m, fx.Alice);
        var splitService = CreateSplitService(fx);

        var result = await splitService.ApplySplitAsync(request, fx.Alice.Id, 25m, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("at most $20");
    }

    [Fact]
    public async Task ApplySplit_WhenOffererIsNotLockedLeaderOrRunnerUp_Fails()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var request = await AddEmergencyRequestAsync(fx, fx.Bob, amountNeeded: 50m, fx.Alice);
        var splitService = CreateSplitService(fx);

        var result = await splitService.ApplySplitAsync(request, fx.Carol.Id, 25m, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("current and next cycle");
    }

    [Fact]
    public async Task CaptureEligibleOffererUserIds_ReturnsLockedLeaderAndRunnerUp()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var splitService = CreateSplitService(fx);

        var ids = await splitService.CaptureEligibleOffererUserIdsAsync(
            fx.Crew.Id,
            fx.Carol.Id,
            CancellationToken.None);

        ids.Should().BeEquivalentTo([fx.Bob.Id, fx.Alice.Id]);
    }

    [Fact]
    public async Task ApplySplit_UserReport_Emergency100_Splits50Then12_Cycle110_AllowsFurtherSplit()
    {
        // Repro: emergency $100, cycle $110, prior splits $50+$12 — further split of remaining $38 should work.
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 110m);
        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 100m);
        var splitService = CreateSplitService(fx);

        (await splitService.ApplySplitAsync(request, fx.Bob.Id, 50m, CancellationToken.None))
            .Success.Should().BeTrue();
        await fx.Context.SaveChangesAsync();

        (await splitService.ApplySplitAsync(request, fx.Alice.Id, 12m, CancellationToken.None))
            .Success.Should().BeTrue();
        await fx.Context.SaveChangesAsync();

        request.AmountSplitCommitted.Should().Be(62m);
        EmergencyRequestAccounting.GetAmountUncovered(request).Should().Be(38m);

        var eligibility = await splitService.GetViewerSplitEligibilityAsync(
            request, fx.Bob.Id, CancellationToken.None);
        eligibility.CanSplit.Should().BeTrue(because: eligibility.Message);
        eligibility.MaxSplitAmount.Should().Be(38m);

        var result = await splitService.ApplySplitAsync(request, fx.Bob.Id, 38m, CancellationToken.None);
        result.Success.Should().BeTrue(because: result.Message);
    }

    [Fact]
    public async Task GetViewerSplitEligibility_IncludesRequesterRemainingCapacity()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 110m);
        var carolPrimary = await fx.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fx.Carol.Id && c.SeasonStartDate == fx.SeasonStart
            && c.EmergencyRequestId == null && c.EmergencySplitOfferId == null);
        // Requester only has $20 left on their primary after prior reception.
        carolPrimary.CycleReceived = 90m;
        await fx.Context.SaveChangesAsync();

        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 100m);
        var splitService = CreateSplitService(fx);

        var eligibility = await splitService.GetViewerSplitEligibilityAsync(
            request, fx.Bob.Id, CancellationToken.None);

        eligibility.CanSplit.Should().BeTrue(because: eligibility.Message);
        // Must not advertise more than the requester can sacrifice from their cycle.
        eligibility.MaxSplitAmount.Should().Be(20m);

        var over = await splitService.ApplySplitAsync(request, fx.Bob.Id, 50m, CancellationToken.None);
        over.Success.Should().BeFalse();
        over.Message.Should().Contain("requester");

        var ok = await splitService.ApplySplitAsync(request, fx.Bob.Id, 20m, CancellationToken.None);
        ok.Success.Should().BeTrue(because: ok.Message);
    }

    [Fact]
    public async Task GetViewerSplitEligibility_WhenRequesterCurrentCapacityExhausted_UsesNextSeasonRemaining()
    {
        // Cycle $110 with $48 already received → $62 available. Splits $50+$12 exhaust the current
        // primary while $38 of need remains — eligibility should fall through to next-season capacity
        // (same as ApplySplit) rather than advertising uncovered need the current primary cannot cover.
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 110m);
        var carolPrimary = await fx.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fx.Carol.Id && c.SeasonStartDate == fx.SeasonStart
            && c.EmergencyRequestId == null && c.EmergencySplitOfferId == null);
        carolPrimary.CycleReceived = 48m;
        await fx.Context.SaveChangesAsync();

        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 100m);
        var splitService = CreateSplitService(fx);

        (await splitService.ApplySplitAsync(request, fx.Bob.Id, 50m, CancellationToken.None))
            .Success.Should().BeTrue();
        await fx.Context.SaveChangesAsync();
        (await splitService.ApplySplitAsync(request, fx.Alice.Id, 12m, CancellationToken.None))
            .Success.Should().BeTrue();
        await fx.Context.SaveChangesAsync();

        EmergencyRequestAccounting.GetAmountUncovered(request).Should().Be(38m);

        var eligibility = await splitService.GetViewerSplitEligibilityAsync(
            request, fx.Bob.Id, CancellationToken.None);
        eligibility.CanSplit.Should().BeTrue(because: eligibility.Message);
        eligibility.MaxSplitAmount.Should().Be(38m);

        var ok = await splitService.ApplySplitAsync(request, fx.Bob.Id, 38m, CancellationToken.None);
        ok.Success.Should().BeTrue(because: ok.Message);
    }

    [Fact]
    public async Task ApplySplit_UnlockedRequester_NeedExceedsCap_RunnerUpFifty_SucceedsOnSqlite()
    {
        // User report: 4-person crew, cycle $110, unlocked requester asks $150,
        // runner-up offers a $50 split — previously failed to save (Ensure/reorder SaveChanges).
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 110m, useSqlite: true);
        var dave = new User
        {
            Username = "dave",
            Email = "dave@example.com",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow,
            InNeedOfAid = true
        };
        fx.Context.Users.Add(dave);
        await fx.Context.SaveChangesAsync();

        fx.Context.CrewMemberships.Add(new CrewMembership
        {
            UserId = dave.Id,
            CrewId = fx.Crew.Id,
            IsBanned = false,
            JoinedAt = DateTime.UtcNow,
            EstimatedMonthlyContribution = 100m,
            IsSeasonReady = true,
            IsInSeason = true,
            GivingSeasonJoinedAt = fx.SeasonStart,
            IsHonoraryMember = true,
            CurrentPriorityScore = 50m
        });
        fx.Context.SeasonCycles.AddRange(
            new SeasonCycle
            {
                CrewId = fx.Crew.Id,
                UserId = dave.Id,
                SeasonStartDate = fx.SeasonStart,
                CycleCapAtStart = 110m,
                CapIsProvisional = false,
                TotalReceptionAmount = 0m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 0m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 50m,
                ReceptionOrderPosition = 3,
                HasCycleStarted = false
            },
            new SeasonCycle
            {
                CrewId = fx.Crew.Id,
                UserId = dave.Id,
                SeasonStartDate = fx.Crew.NextSeasonStartDate!.Value,
                CycleCapAtStart = 0m,
                CapIsProvisional = true,
                TotalReceptionAmount = 0m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 0m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 50m,
                ReceptionOrderPosition = 3,
                HasCycleStarted = false
            },
            new SeasonCycle
            {
                CrewId = fx.Crew.Id,
                UserId = dave.Id,
                SeasonStartDate = fx.Crew.FollowingSeasonStartDate!.Value,
                CycleCapAtStart = 0m,
                CapIsProvisional = true,
                TotalReceptionAmount = 0m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 0m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 50m,
                ReceptionOrderPosition = 3,
                HasCycleStarted = false
            });
        await fx.Context.SaveChangesAsync();

        // Dave is unlocked (position 3). Locked are Bob (leader) + Alice (runner-up).
        var request = await AddEmergencyRequestAsync(fx, dave, amountNeeded: 150m, fx.Bob, fx.Alice);
        var splitService = CreateSplitService(fx);

        var eligibility = await splitService.GetViewerSplitEligibilityAsync(
            request, fx.Alice.Id, CancellationToken.None);
        eligibility.CanSplit.Should().BeTrue(because: eligibility.Message);
        eligibility.MaxSplitAmount.Should().Be(110m);

        var result = await splitService.ApplySplitAsync(request, fx.Alice.Id, 50m, CancellationToken.None);
        result.Success.Should().BeTrue(because: result.Message);
        await fx.Context.SaveChangesAsync();

        request.AmountSplitCommitted.Should().Be(50m);
        await AssertSplitLinkedAsync(fx, request, fx.Alice.Id, 50m, EmergencyOffererQueueRole.RunnerUp);
    }

    private static EmergencySplitService CreateSplitService(MutualAidSeasonFixture fx) =>
        new(
            new MutualAidRepository(fx.Context),
            new CrewMembershipRepository(fx.Context),
            new EmergencyRequestRepository(fx.Context),
                fx.Service,
                fx.Context);

    private static async Task AssertSplitLinkedAsync(
        MutualAidSeasonFixture fx,
        EmergencyRequest request,
        int offererUserId,
        decimal amount,
        EmergencyOffererQueueRole role)
    {
        var offer = await fx.Context.EmergencySplitOffers.SingleAsync(o => o.EmergencyRequestId == request.Id);
        offer.Amount.Should().Be(amount);
        offer.OffererUserId.Should().Be(offererUserId);
        offer.OffererQueueRole.Should().Be(role);
        offer.OffererPaybackCycleId.Should().NotBeNull();
        offer.RequesterEmergencyCycleId.Should().NotBeNull();

        var payback = await fx.Context.SeasonCycles.SingleAsync(c => c.Id == offer.OffererPaybackCycleId);
        payback.UserId.Should().Be(offererUserId);
        payback.EmergencySplitOfferId.Should().Be(offer.Id);
        payback.EmergencySplitOfferId.Should().BeGreaterThan(0);
        payback.CycleCapAtStart.Should().Be(amount);

        var emergency = await fx.Context.SeasonCycles.SingleAsync(c => c.Id == offer.RequesterEmergencyCycleId);
        emergency.UserId.Should().Be(request.RequesterUserId);
        emergency.EmergencyRequestId.Should().Be(request.Id);
        emergency.CycleCapAtStart.Should().Be(amount);
    }

    private static async Task<EmergencyRequest> AddEmergencyRequestAsync(
        MutualAidSeasonFixture fx,
        User requester,
        decimal amountNeeded,
        params User[] eligibleOfferers)
    {
        IEnumerable<int> eligibleIds = eligibleOfferers.Select(u => u.Id);
        if (!eligibleOfferers.Any() && requester.Id == fx.Carol.Id)
        {
            // Fixture order is Bob, Alice, Carol — both are ahead of Carol by default.
            eligibleIds = new[] { fx.Bob.Id, fx.Alice.Id };
        }

        var request = new EmergencyRequest
        {
            CrewId = fx.Crew.Id,
            RequesterUserId = requester.Id,
            Purpose = "Test emergency",
            AmountNeeded = amountNeeded,
            AmountReceived = 0m,
            AmountSplitCommitted = 0m,
            Status = EmergencyRequestStatus.Open,
            CreatedAt = DateTime.UtcNow,
            SplitEligibleOffererUserIds = EmergencySplitService.FormatEligibleOffererUserIds(eligibleIds)
        };
        fx.Context.EmergencyRequests.Add(request);
        await fx.Context.SaveChangesAsync();
        return request;
    }
}
