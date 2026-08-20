using LiberationFleet.Server.Application.Features.Gifts;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts;

public class CustomGiftRecordingServiceTests
{
    [Fact]
    public async Task RecordAsync_SurvivalThresholdWithOverflow_SplitsIntoSurvivalAndOther()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        await fixture.AddUnsatisfiedThresholdAsync(fixture.Bob, thresholdAmount: 20m);

        var service = HandlerTestFixture.CreateCustomGiftRecordingService(fixture.Context, fixture.Service);

        var (applied, other) = await service.RecordAsync(
            fixture.Crew.Id,
            fixture.Alice.Id,
            fixture.Bob.Id,
            amount: 75m,
            fixture.Platforms["Venmo"].Id,
            middlemanId: null,
            CustomGiftCategory.SurvivalThreshold,
            CancellationToken.None);

        applied.Should().NotBeNull();
        applied!.Amount.Should().Be(20m);
        applied.CustomGiftCategory.Should().Be(CustomGiftCategory.SurvivalThreshold);
        applied.IsSurvivalThreshold.Should().BeTrue();
        applied.CountsTowardReception.Should().BeTrue();

        other.Should().NotBeNull();
        other!.Amount.Should().Be(55m);
        other.CustomGiftCategory.Should().Be(CustomGiftCategory.Other);
        other.CountsTowardReception.Should().BeFalse();

        var threshold = await fixture.Context.MonthlySurvivalThresholds.SingleAsync();
        threshold.ReceivedAmount.Should().Be(20m);
        threshold.Satisfied.Should().BeTrue();

        var gifts = await fixture.Context.Gifts
            .Include(g => g.GiverUser)
            .Include(g => g.RecipientUser)
            .Include(g => g.CrewPaymentPlatform)
            .Where(g => g.Amount > 0m)
            .OrderBy(g => g.Amount)
            .ToListAsync();
        gifts.Should().HaveCount(2);
        GiftMapper.MapGift(gifts[0]).Message.Should().Contain("[Survival threshold]");
        GiftMapper.MapGift(gifts[1]).Message.Should().Contain("[Other]");
    }

    [Fact]
    public async Task RecordAsync_CycleWithOverflow_SplitsIntoCycleAndOther()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 40m);
        var service = HandlerTestFixture.CreateCustomGiftRecordingService(fixture.Context, fixture.Service);

        var (applied, other) = await service.RecordAsync(
            fixture.Crew.Id,
            fixture.Alice.Id,
            fixture.Bob.Id,
            amount: 100m,
            fixture.Platforms["Venmo"].Id,
            middlemanId: null,
            CustomGiftCategory.Cycle,
            CancellationToken.None);

        applied.Should().NotBeNull();
        applied!.Amount.Should().Be(40m);
        applied.CustomGiftCategory.Should().Be(CustomGiftCategory.Cycle);

        other.Should().NotBeNull();
        other!.Amount.Should().Be(60m);
        other.CustomGiftCategory.Should().Be(CustomGiftCategory.Other);

        var cycle = await fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == fixture.Bob.Id && c.SeasonStartDate == fixture.SeasonStart);
        cycle.CycleReceived.Should().Be(40m);
    }

    [Fact]
    public async Task RecordAsync_OtherCategory_CreatesOnlyOtherGift()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        await fixture.AddUnsatisfiedThresholdAsync(fixture.Bob, thresholdAmount: 20m);
        var service = HandlerTestFixture.CreateCustomGiftRecordingService(fixture.Context, fixture.Service);

        var (applied, other) = await service.RecordAsync(
            fixture.Crew.Id,
            fixture.Alice.Id,
            fixture.Bob.Id,
            amount: 30m,
            fixture.Platforms["Venmo"].Id,
            middlemanId: null,
            CustomGiftCategory.Other,
            CancellationToken.None);

        applied.Should().BeNull();
        other.Should().NotBeNull();
        other!.Amount.Should().Be(30m);
        other.CustomGiftCategory.Should().Be(CustomGiftCategory.Other);

        var threshold = await fixture.Context.MonthlySurvivalThresholds.SingleAsync();
        threshold.ReceivedAmount.Should().Be(0m);
    }

    [Fact]
    public void ParseCategory_MapsClientValues()
    {
        CustomGiftRecordingService.ParseCategory("survivalThreshold").Should().Be(CustomGiftCategory.SurvivalThreshold);
        CustomGiftRecordingService.ParseCategory("cycle").Should().Be(CustomGiftCategory.Cycle);
        CustomGiftRecordingService.ParseCategory("emergency").Should().Be(CustomGiftCategory.Emergency);
        CustomGiftRecordingService.ParseCategory("other").Should().Be(CustomGiftCategory.Other);
        CustomGiftRecordingService.ParseCategory(null).Should().Be(CustomGiftCategory.Other);
    }
}
