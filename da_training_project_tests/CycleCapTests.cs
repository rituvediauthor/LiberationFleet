using da_training_project_tests.Support;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace da_training_project_tests;

public class CycleCapTests
{
    [Fact]
    public async Task MemberCycleCap_IsDoubleTheTotalMonthlyCapacity()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, creator) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var bob = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Bob");

        var threeMonthsAgo = DateTime.UtcNow.AddMonths(-2);
        await MutualAidTestFixture.SeedContributionGiftAsync(context, crew.Id, creator.Id, bob.Id, 100m, threeMonthsAgo);
        await MutualAidTestFixture.SeedContributionGiftAsync(context, crew.Id, bob.Id, creator.Id, 80m, threeMonthsAgo);

        var calculationService = MutualAidTestFixture.CreateCalculationService(context);
        var totalMonthlyCapacity = await calculationService.CalculateTotalMonthlyGivingCapacityAsync(crew.Id);
        var memberCycleCap = await calculationService.CalculateCycleCapForMemberAsync(crew.Id, isMember: true);

        memberCycleCap.Should().Be(totalMonthlyCapacity * 2m);
        memberCycleCap.Should().Be(120m);
    }

    [Fact]
    public async Task NonMemberCycleCap_IsHalfTheTotalMonthlyCapacity()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, creator) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var bob = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Bob");

        var threeMonthsAgo = DateTime.UtcNow.AddMonths(-2);
        await MutualAidTestFixture.SeedContributionGiftAsync(context, crew.Id, creator.Id, bob.Id, 100m, threeMonthsAgo);
        await MutualAidTestFixture.SeedContributionGiftAsync(context, crew.Id, bob.Id, creator.Id, 80m, threeMonthsAgo);

        var calculationService = MutualAidTestFixture.CreateCalculationService(context);
        var totalMonthlyCapacity = await calculationService.CalculateTotalMonthlyGivingCapacityAsync(crew.Id);
        var nonMemberCycleCap = await calculationService.CalculateCycleCapForMemberAsync(crew.Id, isMember: false);

        nonMemberCycleCap.Should().Be(totalMonthlyCapacity / 2m);
        nonMemberCycleCap.Should().Be(30m);
    }

    [Fact]
    public async Task MembershipStatus_ContributedThisSeason_IsMember()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, creator) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var bob = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Bob");

        await MutualAidTestFixture.SeedContributionGiftAsync(
            context, crew.Id, bob.Id, creator.Id, 50m, crew.CurrentSeasonStartDate.AddDays(1));

        var calculationService = MutualAidTestFixture.CreateCalculationService(context);
        var isMember = await calculationService.IsMemberAsync(bob.Id, crew.Id);

        isMember.Should().BeTrue();
    }

    [Fact]
    public async Task MembershipStatus_ContributedLastSeason_IsMember()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, creator) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var bob = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Bob");

        var previousSeasonGiftDate = crew.CurrentSeasonStartDate.AddDays(-30);
        await MutualAidTestFixture.SeedContributionGiftAsync(
            context, crew.Id, bob.Id, creator.Id, 30m, previousSeasonGiftDate);

        var calculationService = MutualAidTestFixture.CreateCalculationService(context);
        var isMember = await calculationService.IsMemberAsync(bob.Id, crew.Id);

        isMember.Should().BeTrue();
    }

    [Fact]
    public async Task MembershipStatus_NeverContributed_IsNotMember()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, _) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var bob = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Bob");

        var calculationService = MutualAidTestFixture.CreateCalculationService(context);
        var isMember = await calculationService.IsMemberAsync(bob.Id, crew.Id);

        isMember.Should().BeFalse();
    }

    [Fact]
    public async Task CycleGifts_CannotExceedCycleCapForReception()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var recipient = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Receiver");

        const decimal cycleCap = 200m;
        var seasonStart = crew.CurrentSeasonStartDate;
        context.SeasonCycles.Add(new SeasonCycle
        {
            CrewId = crew.Id,
            UserId = recipient.Id,
            SeasonStartDate = seasonStart,
            CycleCapAtStart = cycleCap,
            TotalReceptionAmount = 0m,
            SurvivalThresholdReceived = 0m,
            CycleReceived = 0m,
            CycleCompleted = false,
            PriorityScoreAtSeasonStart = 100m,
            ReceptionOrderPosition = 1
        });
        await context.SaveChangesAsync();

        var handler = MutualAidTestFixture.CreateRecordGiftHandler(context, giver.Id);
        var result = await handler.Handle(
            new LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGift.RecordGiftCommand(
                250m, 1, recipient.Id, null, null, IsSurvivalThreshold: false),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        var cycle = await context.SeasonCycles
            .Where(s => s.UserId == recipient.Id && s.SeasonStartDate == seasonStart)
            .SingleAsync();
        cycle.CycleReceived.Should().BeLessThanOrEqualTo(cycleCap);
    }

    [Fact]
    public async Task SurvivalThresholdGifts_CanExceedCycleCapReception()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var recipient = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "ThresholdUser", needsSurvivalAid: true);

        const decimal cycleCap = 200m;
        var seasonStart = crew.CurrentSeasonStartDate;
        context.SeasonCycles.Add(new SeasonCycle
        {
            CrewId = crew.Id,
            UserId = recipient.Id,
            SeasonStartDate = seasonStart,
            CycleCapAtStart = cycleCap,
            TotalReceptionAmount = cycleCap,
            SurvivalThresholdReceived = 0m,
            CycleReceived = cycleCap,
            CycleCompleted = true,
            CycleCompletedAt = DateTime.UtcNow.AddDays(-2),
            PriorityScoreAtSeasonStart = 100m,
            ReceptionOrderPosition = 1
        });

        var now = DateTime.UtcNow;
        context.MonthlySurvivalThresholds.Add(new MonthlySurvivalThreshold
        {
            CrewId = crew.Id,
            UserId = recipient.Id,
            Year = now.Year,
            Month = now.Month,
            ThresholdAmount = 50m,
            ReceivedAmount = 0m,
            ReceptionOrderPosition = 1,
            Satisfied = false
        });
        await context.SaveChangesAsync();

        var handler = MutualAidTestFixture.CreateRecordGiftHandler(context, giver.Id);
        var result = await handler.Handle(
            new LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGift.RecordGiftCommand(
                50m, 1, recipient.Id, null, null, IsSurvivalThreshold: true),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        var cycle = await context.SeasonCycles
            .Where(s => s.UserId == recipient.Id && s.SeasonStartDate == seasonStart)
            .SingleAsync();
        cycle.TotalReceptionAmount.Should().BeGreaterThan(cycleCap);
        cycle.SurvivalThresholdReceived.Should().Be(50m);
    }

    [Fact]
    public async Task Season_EndsWhenAllCrewmatesCompleteCycle()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, _) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var alice = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Alice");
        var bob = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Bob");

        var seasonStart = crew.CurrentSeasonStartDate;
        context.SeasonCycles.AddRange(
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = alice.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 200m,
                TotalReceptionAmount = 210m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 210m,
                CycleCompleted = true,
                CycleCompletedAt = DateTime.UtcNow.AddDays(-10),
                PriorityScoreAtSeasonStart = 300m,
                ReceptionOrderPosition = 1
            },
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = bob.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 200m,
                TotalReceptionAmount = 205m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 205m,
                CycleCompleted = true,
                CycleCompletedAt = DateTime.UtcNow.AddDays(-5),
                PriorityScoreAtSeasonStart = 200m,
                ReceptionOrderPosition = 2
            });
        await context.SaveChangesAsync();

        var receptionService = MutualAidTestFixture.CreateReceptionOrderService(context);
        await receptionService.CheckAndStartNewSeasonAsync(crew.Id);

        var reloadedCrew = await context.Crews.SingleAsync(c => c.Id == crew.Id);
        reloadedCrew.CurrentSeasonStartDate.Should().BeAfter(seasonStart);

        var newSeasonCycles = await context.SeasonCycles
            .Where(s => s.CrewId == crew.Id && s.SeasonStartDate == reloadedCrew.CurrentSeasonStartDate)
            .ToListAsync();
        newSeasonCycles.Should().HaveCount(3);
    }

    [Fact]
    public async Task MonthlyGivingCapacity_IsAverageOfLastThreeMonths()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, creator) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var bob = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Bob");

        var baseDate = DateTime.UtcNow.AddMonths(-2);
        await MutualAidTestFixture.SeedContributionGiftAsync(context, crew.Id, creator.Id, bob.Id, 90m, baseDate);
        await MutualAidTestFixture.SeedContributionGiftAsync(context, crew.Id, creator.Id, bob.Id, 60m, baseDate.AddDays(10));
        await MutualAidTestFixture.SeedContributionGiftAsync(context, crew.Id, creator.Id, bob.Id, 30m, baseDate.AddDays(20));

        var calculationService = MutualAidTestFixture.CreateCalculationService(context);
        var monthlyCapacity = await calculationService.GetUserMonthlyGivingCapacityAsync(creator.Id, crew.Id);

        monthlyCapacity.Should().Be(60m);
    }

    [Fact]
    public async Task ActiveCycleHolder_RemainsAheadAfterOtherCrewmateEmergencyEscalation()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var alice = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Alice", emergencyLevel: 3);
        var bob = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Bob", emergencyLevel: 2);

        var seasonStart = crew.CurrentSeasonStartDate;
        context.SeasonCycles.AddRange(
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = alice.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 200m,
                TotalReceptionAmount = 100m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 100m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 500m,
                ReceptionOrderPosition = 1
            },
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = bob.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 200m,
                TotalReceptionAmount = 0m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 0m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 300m,
                ReceptionOrderPosition = 2
            });
        await context.SaveChangesAsync();

        bob.EmergencyLevel = 10;
        await context.SaveChangesAsync();

        var order = await MutualAidTestFixture.CreateReceptionOrderService(context)
            .GetOrderedRecipientsAsync(crew.Id, giver.Id);

        var cycleEntries = order.Where(r => !r.IsSurvivalThreshold).ToList();
        cycleEntries.Should().HaveCountGreaterThanOrEqualTo(2);
        cycleEntries[0].UserId.Should().Be(alice.Id);
        cycleEntries[1].UserId.Should().Be(bob.Id);
    }
}
