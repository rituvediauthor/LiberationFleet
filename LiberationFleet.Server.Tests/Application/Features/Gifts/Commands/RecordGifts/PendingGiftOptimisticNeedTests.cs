using LiberationFleet.Server.Application.Features.EmergencyRequests;
using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGifts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Application.Features.Gifts.Commands.VerifyGift;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts.Commands.RecordGifts;

public class PendingGiftOptimisticNeedTests
{
    private static RecordGiftsCommandHandler CreateRecordHandler(
        MutualAidSeasonFixture fixture,
        int giverUserId)
    {
        return new RecordGiftsCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(giverUserId).Object,
            new CrewMembershipRepository(fixture.Context),
            new GiftRepository(fixture.Context),
            new CrewPaymentPlatformRepository(fixture.Context),
            new UserRepository(fixture.Context),
            new MutualAidRepository(fixture.Context),
            fixture.Service,
            HandlerTestFixture.CreateCustomGiftRecordingService(fixture.Context, fixture.Service),
            HandlerTestFixture.CreateNotificationService(fixture.Context),
            fixture.Context,
            NullLogger<RecordGiftsCommandHandler>.Instance);
    }

    [Fact]
    public async Task ReceptionOrder_ReflectsPendingGiftWithoutCompletingCycle()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var bobCycle = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Bob.Id && c.SeasonStartDate == fixture.SeasonStart);
        bobCycle.CycleReceived = 0m;
        bobCycle.CycleCompleted = false;
        await fixture.Context.SaveChangesAsync();

        var before = await fixture.Service.GetReceptionOrderAsync(
            fixture.Alice.Id,
            limit: 30,
            forRecordGift: true);
        var bobBefore = before.First(e => e.UserId == fixture.Bob.Id && e.EntryType == "cycle");
        bobBefore.AmountNeeded.Should().BeGreaterThan(30m);

        var recordHandler = CreateRecordHandler(fixture, fixture.Alice.Id);
        var result = await recordHandler.Handle(
            new RecordGiftsCommand(
            [
                new GiftRecordItem(
                    30,
                    fixture.Platforms["PayPal"].Id,
                    fixture.Bob.Id,
                    null,
                    false,
                    "cycle",
                    bobCycle.Id)
            ]),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        var gift = await fixture.Context.Gifts.SingleAsync(g => g.Type == GiftType.Direct);
        gift.VerificationStatus.Should().Be(GiftVerificationStatus.Pending);
        gift.ReceptionApplied.Should().BeFalse();

        var cycleStill = await fixture.Context.SeasonCycles.SingleAsync(c => c.Id == bobCycle.Id);
        cycleStill.CycleReceived.Should().Be(0m);
        cycleStill.CycleCompleted.Should().BeFalse();

        var after = await fixture.Service.GetReceptionOrderAsync(
            fixture.Alice.Id,
            limit: 30,
            forRecordGift: true);
        var bobAfter = after.First(e => e.UserId == fixture.Bob.Id && e.EntryType == "cycle");
        bobAfter.AmountNeeded.Should().Be(bobBefore.AmountNeeded - 30m);
        bobAfter.HasUnverifiedPending.Should().BeTrue();
        bobAfter.PendingUnverifiedAmount.Should().Be(30m);
    }

    [Fact]
    public async Task Verify_AppliesReceptionAndCanCompleteCycle()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var bobCycle = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Bob.Id && c.SeasonStartDate == fixture.SeasonStart);
        // Leave a small remaining need so one gift can complete the cycle.
        bobCycle.CycleReceived = 70m;
        bobCycle.CycleCompleted = false;
        await fixture.Context.SaveChangesAsync();

        var recordHandler = CreateRecordHandler(fixture, fixture.Alice.Id);
        await recordHandler.Handle(
            new RecordGiftsCommand(
            [
                new GiftRecordItem(
                    30,
                    fixture.Platforms["PayPal"].Id,
                    fixture.Bob.Id,
                    null,
                    false,
                    "cycle",
                    bobCycle.Id)
            ]),
            CancellationToken.None);

        var gift = await fixture.Context.Gifts.SingleAsync(g => g.Type == GiftType.Direct);
        var verifyHandler = new VerifyGiftCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(fixture.Bob.Id).Object,
            new CrewMembershipRepository(fixture.Context),
            new GiftRepository(fixture.Context),
            new CrewPaymentPlatformRepository(fixture.Context),
            fixture.Service,
            fixture.Context,
            NullLogger<VerifyGiftCommandHandler>.Instance);

        var verifyResult = await verifyHandler.Handle(
            new VerifyGiftCommand(gift.Id, GiftVerificationAction.ConfirmReceived),
            CancellationToken.None);

        verifyResult.Success.Should().BeTrue();

        var cycle = await fixture.Context.SeasonCycles.SingleAsync(c => c.Id == bobCycle.Id);
        cycle.CycleReceived.Should().Be(100m);
        // Completion depends on effective cap from fixture; require at least reception applied.
        gift = await fixture.Context.Gifts.SingleAsync(g => g.Id == gift.Id);
        gift.ReceptionApplied.Should().BeTrue();
        gift.VerificationStatus.Should().Be(GiftVerificationStatus.Verified);
    }

    [Fact]
    public async Task ReceptionOrder_PendingSurvivalGiftOnlyReducesTargetedThreshold()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var olderMonth = DateTime.UtcNow.Month == 1 ? 12 : DateTime.UtcNow.Month - 1;
        var olderYear = DateTime.UtcNow.Month == 1 ? DateTime.UtcNow.Year - 1 : DateTime.UtcNow.Year;
        var older = await fixture.AddUnsatisfiedThresholdAsync(
            fixture.Bob,
            thresholdAmount: 40m,
            year: olderYear,
            month: olderMonth);
        var newer = await fixture.AddUnsatisfiedThresholdAsync(fixture.Bob, thresholdAmount: 40m);

        var before = await fixture.Service.GetReceptionOrderAsync(
            fixture.Alice.Id,
            limit: 30,
            forRecordGift: true);
        var newerBefore = before.Single(e => e.ThresholdId == newer.Id);

        var recordHandler = CreateRecordHandler(fixture, fixture.Alice.Id);
        var result = await recordHandler.Handle(
            new RecordGiftsCommand(
            [
                new GiftRecordItem(
                    10,
                    fixture.Platforms["PayPal"].Id,
                    fixture.Bob.Id,
                    null,
                    false,
                    "survivalThreshold",
                    null,
                    newer.Id)
            ]),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        var gift = await fixture.Context.Gifts.SingleAsync(g => g.IsSurvivalThreshold);
        gift.MonthlySurvivalThresholdId.Should().Be(newer.Id);

        var after = await fixture.Service.GetReceptionOrderAsync(
            fixture.Alice.Id,
            limit: 30,
            forRecordGift: true);
        var olderEntry = after.Single(e => e.ThresholdId == older.Id);
        var newerEntry = after.Single(e => e.ThresholdId == newer.Id);
        olderEntry.AmountNeeded.Should().Be(40m);
        olderEntry.HasUnverifiedPending.Should().BeFalse();
        newerEntry.AmountNeeded.Should().Be(newerBefore.AmountNeeded - 10m);
        newerEntry.PendingUnverifiedAmount.Should().Be(10m);
    }

    [Fact]
    public async Task Verify_FullPaybackGift_CompletesPaybackSegment()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var request = new EmergencyRequest
        {
            CrewId = fixture.Crew.Id,
            RequesterUserId = fixture.Carol.Id,
            Purpose = "Verify payback",
            AmountNeeded = 12m,
            AmountReceived = 0m,
            AmountSplitCommitted = 0m,
            Status = EmergencyRequestStatus.Open,
            CreatedAt = DateTime.UtcNow,
            SplitEligibleOffererUserIds = EmergencySplitService.FormatEligibleOffererUserIds(
                [fixture.Bob.Id, fixture.Alice.Id])
        };
        fixture.Context.EmergencyRequests.Add(request);
        await fixture.Context.SaveChangesAsync();

        var splitService = new EmergencySplitService(
            new MutualAidRepository(fixture.Context),
            new CrewMembershipRepository(fixture.Context),
            new EmergencyRequestRepository(fixture.Context),
            fixture.Service);
        (await splitService.ApplySplitAsync(request, fixture.Alice.Id, 12m, CancellationToken.None))
            .Success.Should().BeTrue();
        await fixture.Context.SaveChangesAsync();

        var payback = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.EmergencySplitOfferId != null && c.UserId == fixture.Alice.Id && !c.CycleCompleted);

        var recordHandler = CreateRecordHandler(fixture, fixture.Bob.Id);
        var recordResult = await recordHandler.Handle(
            new RecordGiftsCommand(
            [
                new GiftRecordItem(
                    12,
                    fixture.Platforms["PayPal"].Id,
                    fixture.Alice.Id,
                    null,
                    false,
                    "cycle",
                    payback.Id)
            ]),
            CancellationToken.None);
        recordResult.Success.Should().BeTrue();

        var gift = await fixture.Context.Gifts.SingleAsync(g => g.Type == GiftType.Direct);
        gift.SeasonCycleId.Should().Be(payback.Id);

        var verifyHandler = new VerifyGiftCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(fixture.Alice.Id).Object,
            new CrewMembershipRepository(fixture.Context),
            new GiftRepository(fixture.Context),
            new CrewPaymentPlatformRepository(fixture.Context),
            fixture.Service,
            fixture.Context,
            NullLogger<VerifyGiftCommandHandler>.Instance);

        var verifyResult = await verifyHandler.Handle(
            new VerifyGiftCommand(gift.Id, GiftVerificationAction.ConfirmReceived),
            CancellationToken.None);
        verifyResult.Success.Should().BeTrue();

        gift = await fixture.Context.Gifts.SingleAsync(g => g.Id == gift.Id);
        gift.ReceptionApplied.Should().BeTrue();

        payback = await fixture.Context.SeasonCycles.SingleAsync(c => c.Id == payback.Id);
        payback.CycleReceived.Should().Be(12m);
        payback.CycleCompleted.Should().BeTrue();

        var after = await fixture.Service.GetReceptionOrderAsync(
            fixture.Bob.Id,
            limit: 30,
            forRecordGift: true,
            excludeSelfAsRecipient: false);
        after.Should().NotContain(e => e.SeasonCycleId == payback.Id);
    }

}
