using LiberationFleet.Server.Application.Features.EmergencyRequests;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Features.EmergencyRequests;

public class EmergencyReconciliationServiceTests
{
    [Fact]
    public async Task ApplyDirectGift_WithOpenEmergencyCycle_FillsCycleBeforeUncovered()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 100m);
        var splitService = CreateSplitService(fx);
        var reconciliation = CreateReconciliationService(fx);

        (await splitService.ApplySplitAsync(request, fx.Alice.Id, 50m, CancellationToken.None)).Success.Should().BeTrue();
        await fx.Context.SaveChangesAsync();

        request = (await ReloadRequestAsync(fx, request.Id))!;
        request.AmountSplitCommitted.Should().Be(50m);
        request.AmountReceived.Should().Be(0m);
        EmergencyRequestAccounting.GetAmountRemainingToReceive(request).Should().Be(100m);
        EmergencyRequestAccounting.GetAmountUncovered(request).Should().Be(50m);

        var result = await reconciliation.ApplyDirectGiftAsync(request, 75m, CancellationToken.None);
        await fx.Context.SaveChangesAsync();

        result.AmountAppliedToNeed.Should().Be(75m);
        result.OverflowAmount.Should().Be(0m);
        // $50 filled the emergency cycle; $25 covered uncovered cash need.
        request.AmountReceived.Should().Be(75m);
        request.AmountSplitCommitted.Should().Be(0m);
        request.Status.Should().Be(EmergencyRequestStatus.Open);
        EmergencyRequestAccounting.GetAmountUncovered(request).Should().Be(25m);
        EmergencyRequestAccounting.GetAmountRemainingToReceive(request).Should().Be(25m);

        var emergencySegment = await fx.Context.SeasonCycles.SingleAsync(c =>
            c.EmergencyRequestId == request.Id);
        emergencySegment.CycleReceived.Should().Be(50m);
        emergencySegment.CycleCompleted.Should().BeTrue();

        var split = await fx.Context.EmergencySplitOffers.SingleAsync(o => o.EmergencyRequestId == request.Id);
        split.Amount.Should().Be(0m);
        split.IsCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyDirectGift_DoesNotFulfillOnSplitAlone()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 100m);
        var splitService = CreateSplitService(fx);

        (await splitService.ApplySplitAsync(request, fx.Alice.Id, 100m, CancellationToken.None)).Success.Should().BeTrue();
        await fx.Context.SaveChangesAsync();

        request = (await ReloadRequestAsync(fx, request.Id))!;
        request.AmountSplitCommitted.Should().Be(100m);
        request.AmountReceived.Should().Be(0m);
        EmergencyRequestAccounting.GetAmountRemainingToReceive(request).Should().Be(100m);
        request.Status.Should().Be(EmergencyRequestStatus.Open);
    }

    [Fact]
    public async Task ApplyDirectGift_FillsEmergencyCyclesInReceptionOrder()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 100m);
        var splitService = CreateSplitService(fx);
        var reconciliation = CreateReconciliationService(fx);

        (await splitService.ApplySplitAsync(request, fx.Alice.Id, 40m, CancellationToken.None)).Success.Should().BeTrue();
        (await splitService.ApplySplitAsync(request, fx.Bob.Id, 60m, CancellationToken.None)).Success.Should().BeTrue();
        await fx.Context.SaveChangesAsync();

        request = (await ReloadRequestAsync(fx, request.Id))!;
        request.AmountSplitCommitted.Should().Be(100m);
        EmergencyRequestAccounting.GetAmountRemainingToReceive(request).Should().Be(100m);

        await reconciliation.ApplyDirectGiftAsync(request, 75m, CancellationToken.None);
        await fx.Context.SaveChangesAsync();

        var segments = await fx.Context.SeasonCycles
            .Where(c => c.EmergencyRequestId == request.Id)
            .OrderBy(c => c.ReceptionOrderPosition)
            .ToListAsync();
        segments.Should().HaveCount(2);
        segments[0].CycleReceived.Should().Be(segments[0].CycleCapAtStart);
        segments[0].CycleCompleted.Should().BeTrue();
        var secondTake = 75m - segments[0].CycleCapAtStart;
        segments[1].CycleReceived.Should().Be(secondTake);
        segments[1].CycleCompleted.Should().Be(secondTake >= segments[1].CycleCapAtStart);

        request.AmountReceived.Should().Be(75m);
        EmergencyRequestAccounting.GetAmountRemainingToReceive(request).Should().Be(25m);
    }

    [Fact]
    public async Task ApplyDirectGift_WhenFullySplit_FillsOpenCycleWithoutReopeningUncovered()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 110m);
        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 100m);
        var splitService = CreateSplitService(fx);
        var reconciliation = CreateReconciliationService(fx);

        (await splitService.ApplySplitAsync(request, fx.Bob.Id, 100m, CancellationToken.None)).Success.Should().BeTrue();
        await fx.Context.SaveChangesAsync();

        request = (await ReloadRequestAsync(fx, request.Id))!;
        request.AmountSplitCommitted.Should().Be(100m);
        EmergencyRequestAccounting.GetAmountUncovered(request).Should().Be(0m);
        EmergencyRequestAccounting.GetAmountRemainingToReceive(request).Should().Be(100m);

        var result = await reconciliation.ApplyDirectGiftAsync(request, 50m, CancellationToken.None);
        await fx.Context.SaveChangesAsync();

        result.AmountAppliedToNeed.Should().Be(50m);
        result.OverflowAmount.Should().Be(0m);
        request.AmountReceived.Should().Be(50m);
        request.AmountSplitCommitted.Should().Be(50m);
        EmergencyRequestAccounting.GetAmountUncovered(request).Should().Be(0m);
        EmergencyRequestAccounting.GetAmountRemainingToReceive(request).Should().Be(50m);

        var eligibility = await splitService.GetViewerSplitEligibilityAsync(
            request, fx.Alice.Id, CancellationToken.None);
        eligibility.CanSplit.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyDirectGift_OverflowBeyondRequestRemaining_EvenWhenCycleRoomLarger()
    {
        // Request has $40 left to receive but emergency cycle still has $50 room (e.g. prior
        // uncovered cash). A $50 direct gift must apply $40 and overflow $10 — not fill the cycle.
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 50m);
        var splitService = CreateSplitService(fx);
        var reconciliation = CreateReconciliationService(fx);

        (await splitService.ApplySplitAsync(request, fx.Alice.Id, 50m, CancellationToken.None)).Success.Should().BeTrue();
        await fx.Context.SaveChangesAsync();

        request = (await ReloadRequestAsync(fx, request.Id))!;
        // Prior uncovered cash reduced request remaining without filling the cycle.
        request.AmountReceived = 10m;
        request.AmountSplitCommitted = 40m;
        var emergency = await fx.Context.SeasonCycles.SingleAsync(c =>
            c.EmergencyRequestId == request.Id && !c.CycleCompleted);
        emergency.CycleCapAtStart = 50m;
        emergency.CycleReceived = 0m;
        await fx.Context.SaveChangesAsync();

        EmergencyRequestAccounting.GetAmountRemainingToReceive(request).Should().Be(40m);

        var result = await reconciliation.ApplyDirectGiftAsync(request, 50m, CancellationToken.None);
        await fx.Context.SaveChangesAsync();

        result.AmountAppliedToNeed.Should().Be(40m);
        result.OverflowAmount.Should().Be(10m);
        request.AmountReceived.Should().Be(50m);
        emergency = await fx.Context.SeasonCycles.SingleAsync(c => c.Id == emergency.Id);
        emergency.CycleReceived.Should().Be(40m);
        emergency.CycleCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyDirectGift_WithoutSplits_BurnsUncoveredOnly()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 100m);
        var reconciliation = CreateReconciliationService(fx);

        var result = await reconciliation.ApplyDirectGiftAsync(request, 40m, CancellationToken.None);
        await fx.Context.SaveChangesAsync();

        result.AmountAppliedToNeed.Should().Be(40m);
        request.AmountReceived.Should().Be(40m);
        request.AmountSplitCommitted.Should().Be(0m);
        EmergencyRequestAccounting.GetAmountRemainingToReceive(request).Should().Be(60m);
    }

    [Fact]
    public async Task ApplyDirectGift_Overflow_IsReturnedForUncategorizedGift()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 100m);
        var reconciliation = CreateReconciliationService(fx);

        request.AmountReceived = 100m;
        request.Status = EmergencyRequestStatus.Fulfilled;
        await fx.Context.SaveChangesAsync();

        var result = await reconciliation.ApplyDirectGiftAsync(request, 25m, CancellationToken.None);

        result.AmountAppliedToNeed.Should().Be(0m);
        result.OverflowAmount.Should().Be(25m);
    }

    private static EmergencySplitService CreateSplitService(MutualAidSeasonFixture fx) =>
        new(
            new MutualAidRepository(fx.Context),
            new CrewMembershipRepository(fx.Context),
            new EmergencyRequestRepository(fx.Context),
                fx.Service,
                fx.Context);

    private static EmergencyReconciliationService CreateReconciliationService(MutualAidSeasonFixture fx) =>
        new(new MutualAidRepository(fx.Context));

    private static Task<EmergencyRequest?> ReloadRequestAsync(MutualAidSeasonFixture fx, int requestId) =>
        fx.Context.EmergencyRequests
            .Include(r => r.SplitOffers)
            .FirstOrDefaultAsync(r => r.Id == requestId);

    private static async Task<EmergencyRequest> AddEmergencyRequestAsync(
        MutualAidSeasonFixture fx,
        User requester,
        decimal amountNeeded,
        params User[] eligibleOfferers)
    {
        IEnumerable<int> eligibleIds = eligibleOfferers.Select(u => u.Id);
        if (!eligibleOfferers.Any() && requester.Id == fx.Carol.Id)
        {
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
