using da_training_project_tests.Support;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace da_training_project_tests;

public class SurvivalThresholdTests
{
    [Fact]
    public async Task SurvivalThresholdAmount_CalculatesCorrectly()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, creator) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var bob = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Bob", needsSurvivalAid: true);
        var charlie = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Charlie", needsSurvivalAid: true);

        var threeMonthsAgo = DateTime.UtcNow.AddMonths(-2);
        await MutualAidTestFixture.SeedContributionGiftAsync(context, crew.Id, creator.Id, bob.Id, 90m, threeMonthsAgo);
        await MutualAidTestFixture.SeedContributionGiftAsync(context, crew.Id, bob.Id, creator.Id, 60m, threeMonthsAgo);
        await MutualAidTestFixture.SeedContributionGiftAsync(context, crew.Id, charlie.Id, creator.Id, 30m, threeMonthsAgo);

        var calculationService = MutualAidTestFixture.CreateCalculationService(context);
        var survivalThreshold = await calculationService.CalculateSurvivalThresholdAmountAsync(crew.Id);

        survivalThreshold.Should().Be(15m);
    }

    [Fact]
    public async Task SurvivalThresholdRecipients_OrderedByPriorityScore_AtMonthStart()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, _) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var alice = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Alice", emergencyLevel: 3, needsSurvivalAid: true);
        var bob = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Bob", emergencyLevel: 2, needsSurvivalAid: true);
        var charlie = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Charlie", emergencyLevel: 5, needsSurvivalAid: true);

        var threeMonthsAgo = DateTime.UtcNow.AddMonths(-2);
        await MutualAidTestFixture.SeedContributionGiftAsync(context, crew.Id, charlie.Id, alice.Id, 300m, threeMonthsAgo);
        await MutualAidTestFixture.SeedContributionGiftAsync(context, crew.Id, alice.Id, bob.Id, 150m, threeMonthsAgo);
        await MutualAidTestFixture.SeedContributionGiftAsync(context, crew.Id, bob.Id, alice.Id, 75m, threeMonthsAgo);

        var receptionService = MutualAidTestFixture.CreateReceptionOrderService(context);
        await receptionService.ProcessSurvivalThresholdsForNewMonthAsync(crew.Id);

        var now = DateTime.UtcNow;
        var thresholds = await context.MonthlySurvivalThresholds
            .Where(m => m.CrewId == crew.Id && m.Year == now.Year && m.Month == now.Month)
            .OrderBy(m => m.ReceptionOrderPosition)
            .ToListAsync();

        thresholds.Should().HaveCount(3);
        thresholds[0].UserId.Should().Be(charlie.Id);
        thresholds[1].UserId.Should().Be(alice.Id);
        thresholds[2].UserId.Should().Be(bob.Id);
    }

    [Fact]
    public async Task SurvivalThreshold_NeedAmount_EqualsThresholdMinusReceived()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var recipient = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "NeedCalc", needsSurvivalAid: true);

        var now = DateTime.UtcNow;
        context.MonthlySurvivalThresholds.Add(new MonthlySurvivalThreshold
        {
            CrewId = crew.Id,
            UserId = recipient.Id,
            Year = now.Year,
            Month = now.Month,
            ThresholdAmount = 22m,
            ReceivedAmount = 3m,
            ReceptionOrderPosition = 1,
            Satisfied = false
        });
        await context.SaveChangesAsync();

        var order = await MutualAidTestFixture.CreateReceptionOrderService(context)
            .GetOrderedRecipientsAsync(crew.Id, giver.Id);

        var entry = order.Single(r => r.UserId == recipient.Id && r.IsSurvivalThreshold);
        entry.AmountNeeded.Should().Be(19m);
    }

    [Fact]
    public async Task NewMonth_ExistingUnsatisfiedThresholds_ComeBeforeNewOnes()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var savannah = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Savannah", emergencyLevel: 3, needsSurvivalAid: true);
        var dave = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Dave", emergencyLevel: 2, needsSurvivalAid: true);

        var now = DateTime.UtcNow;
        var previousMonth = now.Month == 1 ? 12 : now.Month - 1;
        var previousYear = now.Month == 1 ? now.Year - 1 : now.Year;

        context.MonthlySurvivalThresholds.AddRange(
            new MonthlySurvivalThreshold
            {
                CrewId = crew.Id,
                UserId = savannah.Id,
                Year = previousYear,
                Month = previousMonth,
                ThresholdAmount = 22m,
                ReceivedAmount = 3m,
                ReceptionOrderPosition = 1,
                Satisfied = false
            },
            new MonthlySurvivalThreshold
            {
                CrewId = crew.Id,
                UserId = dave.Id,
                Year = previousYear,
                Month = previousMonth,
                ThresholdAmount = 22m,
                ReceivedAmount = 0m,
                ReceptionOrderPosition = 2,
                Satisfied = false
            },
            new MonthlySurvivalThreshold
            {
                CrewId = crew.Id,
                UserId = savannah.Id,
                Year = now.Year,
                Month = now.Month,
                ThresholdAmount = 22m,
                ReceivedAmount = 0m,
                ReceptionOrderPosition = 3,
                Satisfied = false
            },
            new MonthlySurvivalThreshold
            {
                CrewId = crew.Id,
                UserId = dave.Id,
                Year = now.Year,
                Month = now.Month,
                ThresholdAmount = 22m,
                ReceivedAmount = 0m,
                ReceptionOrderPosition = 4,
                Satisfied = false
            });
        await context.SaveChangesAsync();

        var receptionService = MutualAidTestFixture.CreateReceptionOrderService(context);
        var order = await receptionService.GetOrderedRecipientsAsync(crew.Id, giver.Id, limit: 10);

        var thresholdEntries = order.Where(r => r.IsSurvivalThreshold).ToList();
        thresholdEntries.Should().HaveCountGreaterThanOrEqualTo(4);
        thresholdEntries[0].UserId.Should().Be(savannah.Id);
        thresholdEntries[0].AmountNeeded.Should().Be(19m);
        thresholdEntries[1].UserId.Should().Be(dave.Id);
        thresholdEntries[2].UserId.Should().Be(savannah.Id);
        thresholdEntries[3].UserId.Should().Be(dave.Id);
    }

    [Fact]
    public async Task SurvivalThreshold_Satisfied_WhenReceivedEqualsThreshold()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var user = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Satisfied", needsSurvivalAid: true);

        var now = DateTime.UtcNow;
        context.MonthlySurvivalThresholds.Add(new MonthlySurvivalThreshold
        {
            CrewId = crew.Id,
            UserId = user.Id,
            Year = now.Year,
            Month = now.Month,
            ThresholdAmount = 22m,
            ReceivedAmount = 22m,
            ReceptionOrderPosition = 1,
            Satisfied = true
        });
        await context.SaveChangesAsync();

        var order = await MutualAidTestFixture.CreateReceptionOrderService(context)
            .GetOrderedRecipientsAsync(crew.Id, giver.Id);

        order.Should().NotContain(r => r.UserId == user.Id && r.IsSurvivalThreshold);
    }

    [Fact]
    public async Task SurvivalThresholdRecipients_OnlyThoseRegistered()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, _) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Alice", needsSurvivalAid: true);
        await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Bob", needsSurvivalAid: false);
        await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Charlie", needsSurvivalAid: true);

        var calculationService = MutualAidTestFixture.CreateCalculationService(context);
        await calculationService.CalculateSurvivalThresholdAmountAsync(crew.Id);

        var survivorCount = await context.Users
            .CountAsync(u => u.NeedsSurvivalAid && u.CrewMemberships.Any(cm => cm.CrewId == crew.Id && !cm.IsBanned));

        survivorCount.Should().Be(2);
        await calculationService.CalculateSurvivalThresholdAmountAsync(crew.Id);
    }
}
