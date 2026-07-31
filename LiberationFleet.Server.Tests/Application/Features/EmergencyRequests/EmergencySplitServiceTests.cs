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
        result.Message.Should().Contain("ahead of the requester");
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

    private static EmergencySplitService CreateSplitService(MutualAidSeasonFixture fx) =>
        new(
            new MutualAidRepository(fx.Context),
            new CrewMembershipRepository(fx.Context),
            new EmergencyRequestRepository(fx.Context),
            fx.Service);

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
            AmountFulfilled = 0m,
            Status = EmergencyRequestStatus.Open,
            CreatedAt = DateTime.UtcNow,
            SplitEligibleOffererUserIds = EmergencySplitService.FormatEligibleOffererUserIds(eligibleIds)
        };
        fx.Context.EmergencyRequests.Add(request);
        await fx.Context.SaveChangesAsync();
        return request;
    }
}
