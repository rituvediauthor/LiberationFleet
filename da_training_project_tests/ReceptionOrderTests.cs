using da_training_project_tests.Support;
using LiberationFleet.Server.Application.Features.Recipients.Queries.GetReceptionOrder;
using LiberationFleet.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace da_training_project_tests;

public class ReceptionOrderTests
{
    [Fact]
    public async Task ReceptionOrder_SurvivalThresholdRecipientsFirst_ThenCycleRecipients()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var savannah = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Savannah", emergencyLevel: 3, needsSurvivalAid: true);
        var dave = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Dave", emergencyLevel: 2, needsSurvivalAid: true);

        var now = DateTime.UtcNow;
        context.MonthlySurvivalThresholds.AddRange(
            new MonthlySurvivalThreshold
            {
                CrewId = crew.Id,
                UserId = savannah.Id,
                Year = now.Year,
                Month = now.Month,
                ThresholdAmount = 22m,
                ReceivedAmount = 3m,
                ReceptionOrderPosition = 1,
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
                ReceptionOrderPosition = 2,
                Satisfied = false
            });

        context.SeasonCycles.AddRange(
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = savannah.Id,
                SeasonStartDate = crew.CurrentSeasonStartDate,
                CycleCapAtStart = 310m,
                TotalReceptionAmount = 110m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 110m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 500m,
                ReceptionOrderPosition = 1
            },
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = dave.Id,
                SeasonStartDate = crew.CurrentSeasonStartDate,
                CycleCapAtStart = 310m,
                TotalReceptionAmount = 0m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 0m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 300m,
                ReceptionOrderPosition = 2
            });
        await context.SaveChangesAsync();

        var handler = MutualAidTestFixture.CreateGetReceptionOrderHandler(context, giver.Id);
        var response = await handler.Handle(new GetReceptionOrderQuery(30), CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Recipients.Should().NotBeEmpty();

        var firstThresholdIndex = response.Recipients.FindIndex(r => r.IsSurvivalThreshold);
        var firstCycleIndex = response.Recipients.FindIndex(r => !r.IsSurvivalThreshold);

        firstThresholdIndex.Should().BeGreaterThanOrEqualTo(0);
        firstCycleIndex.Should().BeGreaterThan(firstThresholdIndex);
    }

    [Fact]
    public async Task ReceptionOrder_LimitedTo30Entries()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var seasonStart = crew.CurrentSeasonStartDate;

        for (var i = 0; i < 35; i++)
        {
            var user = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, $"User{i}");
            context.SeasonCycles.Add(new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = user.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 200m,
                TotalReceptionAmount = 0m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 0m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 100m - i,
                ReceptionOrderPosition = i + 1
            });
        }
        await context.SaveChangesAsync();

        var handler = MutualAidTestFixture.CreateGetReceptionOrderHandler(context, giver.Id);
        var response = await handler.Handle(new GetReceptionOrderQuery(30), CancellationToken.None);

        response.Recipients.Should().HaveCount(30);
    }

    [Fact]
    public async Task ReceptionOrder_EmergencyLevelChange_MovesCrewmateInOrder_ButNotAheadOfActiveCycleHolder()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var charlie = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Charlie", emergencyLevel: 3);
        var bob = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Bob", emergencyLevel: 2);
        var alice = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Alice", emergencyLevel: 1);

        var seasonStart = crew.CurrentSeasonStartDate;
        context.SeasonCycles.AddRange(
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = charlie.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 200m,
                TotalReceptionAmount = 50m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 50m,
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
            },
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = alice.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 200m,
                TotalReceptionAmount = 0m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 0m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 100m,
                ReceptionOrderPosition = 3
            });
        await context.SaveChangesAsync();

        alice.EmergencyLevel = 10;
        await context.SaveChangesAsync();

        var handler = MutualAidTestFixture.CreateGetReceptionOrderHandler(context, giver.Id);
        var response = await handler.Handle(new GetReceptionOrderQuery(30), CancellationToken.None);

        var cycleEntries = response.Recipients.Where(r => !r.IsSurvivalThreshold).ToList();
        cycleEntries[0].UserId.Should().Be(charlie.Id);
        cycleEntries.Select(r => r.UserId).Should().Contain(alice.Id);
        cycleEntries.FindIndex(r => r.UserId == alice.Id).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ReceptionOrder_MovedCrewmate_CannotJumpAheadOfCurrentCycleRecipient()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var current = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Current", emergencyLevel: 2);
        var mover = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Mover", emergencyLevel: 1);

        var seasonStart = crew.CurrentSeasonStartDate;
        context.SeasonCycles.AddRange(
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = current.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 200m,
                TotalReceptionAmount = 100m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 100m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 300m,
                ReceptionOrderPosition = 1
            },
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = mover.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 200m,
                TotalReceptionAmount = 0m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 0m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 100m,
                ReceptionOrderPosition = 2
            });
        await context.SaveChangesAsync();

        mover.EmergencyLevel = 10;
        await context.SaveChangesAsync();

        var handler = MutualAidTestFixture.CreateGetReceptionOrderHandler(context, giver.Id);
        var response = await handler.Handle(new GetReceptionOrderQuery(30), CancellationToken.None);

        var cycleEntries = response.Recipients.Where(r => !r.IsSurvivalThreshold).ToList();
        cycleEntries[0].UserId.Should().Be(current.Id);
        cycleEntries.FindIndex(r => r.UserId == mover.Id).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ReceptionOrder_MatchesProblemTextSavannahDaveExample_AfterMonthRollover()
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
            new MonthlySurvivalThreshold { CrewId = crew.Id, UserId = savannah.Id, Year = previousYear, Month = previousMonth, ThresholdAmount = 22m, ReceivedAmount = 3m, ReceptionOrderPosition = 1, Satisfied = false },
            new MonthlySurvivalThreshold { CrewId = crew.Id, UserId = dave.Id, Year = previousYear, Month = previousMonth, ThresholdAmount = 22m, ReceivedAmount = 0m, ReceptionOrderPosition = 2, Satisfied = false },
            new MonthlySurvivalThreshold { CrewId = crew.Id, UserId = savannah.Id, Year = now.Year, Month = now.Month, ThresholdAmount = 22m, ReceivedAmount = 0m, ReceptionOrderPosition = 3, Satisfied = false },
            new MonthlySurvivalThreshold { CrewId = crew.Id, UserId = dave.Id, Year = now.Year, Month = now.Month, ThresholdAmount = 22m, ReceivedAmount = 0m, ReceptionOrderPosition = 4, Satisfied = false });

        context.SeasonCycles.AddRange(
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = savannah.Id,
                SeasonStartDate = crew.CurrentSeasonStartDate,
                CycleCapAtStart = 310m,
                TotalReceptionAmount = 110m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 110m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 500m,
                ReceptionOrderPosition = 5
            },
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = dave.Id,
                SeasonStartDate = crew.CurrentSeasonStartDate,
                CycleCapAtStart = 310m,
                TotalReceptionAmount = 0m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 0m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 300m,
                ReceptionOrderPosition = 6
            });
        await context.SaveChangesAsync();

        var handler = MutualAidTestFixture.CreateGetReceptionOrderHandler(context, giver.Id);
        var response = await handler.Handle(new GetReceptionOrderQuery(30), CancellationToken.None);

        var labels = response.Recipients.Select(r =>
            $"{r.Username} needs ${r.AmountNeeded:0.##} – {(r.IsSurvivalThreshold ? "Survival threshold" : "Cycle")}").ToList();

        labels[0].Should().Contain("Savannah").And.Contain("19").And.Contain("Survival");
        labels[1].Should().Contain("Dave").And.Contain("22").And.Contain("Survival");
        labels[2].Should().Contain("Savannah").And.Contain("22").And.Contain("Survival");
        labels[3].Should().Contain("Dave").And.Contain("22").And.Contain("Survival");
        labels[4].Should().Contain("Savannah").And.Contain("200").And.Contain("Cycle");
        labels[5].Should().Contain("Dave").And.Contain("310").And.Contain("Cycle");
    }
}
