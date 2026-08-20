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
    public async Task ApplyDirectGift_AfterPartialSplit_CoversUncoveredThenShrinksSplit()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 100m);
        var splitService = CreateSplitService(fx);
        var reconciliation = CreateReconciliationService(fx);

        (await splitService.ApplySplitAsync(request, fx.Alice.Id, 50m, CancellationToken.None)).Success.Should().BeTrue();
        await fx.Context.SaveChangesAsync();

        request = (await ReloadRequestAsync(fx, request.Id))!;
        request.AmountSplitCommitted.Should().Be(50m);
        EmergencyRequestAccounting.GetAmountUncovered(request).Should().Be(50m);

        var result = await reconciliation.ApplyDirectGiftAsync(request, 75m, CancellationToken.None);
        await fx.Context.SaveChangesAsync();

        result.AmountAppliedToNeed.Should().Be(75m);
        result.OverflowAmount.Should().Be(0m);
        request.AmountReceived.Should().Be(50m);
        request.AmountSplitCommitted.Should().Be(25m);
        request.Status.Should().Be(EmergencyRequestStatus.Open);

        var split = await fx.Context.EmergencySplitOffers.SingleAsync(o => o.EmergencyRequestId == request.Id);
        split.Amount.Should().Be(25m);
        split.IsCancelled.Should().BeFalse();

        var alicePrimary = await fx.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fx.Alice.Id
            && c.SeasonStartDate == fx.SeasonStart
            && !c.EmergencyRequestId.HasValue
            && !c.EmergencySplitOfferId.HasValue
            && !c.CycleCompleted);
        alicePrimary.CycleCapAtStart.Should().Be(75m);
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
        request.Status.Should().Be(EmergencyRequestStatus.Open);
    }

    [Fact]
    public async Task ApplyDirectGift_ShrinksRunnerUpSplitBeforeActiveCycleSplit()
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

        await reconciliation.ApplyDirectGiftAsync(request, 75m, CancellationToken.None);
        await fx.Context.SaveChangesAsync();

        var aliceSplit = await fx.Context.EmergencySplitOffers.SingleAsync(o => o.OffererUserId == fx.Alice.Id);
        var bobSplit = await fx.Context.EmergencySplitOffers.SingleAsync(o => o.OffererUserId == fx.Bob.Id);

        aliceSplit.IsCancelled.Should().BeTrue();
        aliceSplit.Amount.Should().Be(0m);
        bobSplit.Amount.Should().Be(25m);
        request.AmountSplitCommitted.Should().Be(25m);
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
            fx.Service);

    private static EmergencyReconciliationService CreateReconciliationService(MutualAidSeasonFixture fx) =>
        new(CreateSplitService(fx));

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
