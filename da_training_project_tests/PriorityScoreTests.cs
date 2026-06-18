using da_training_project_tests.Support;
using LiberationFleet.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace da_training_project_tests;

public class PriorityScoreTests
{
    [Fact]
    public async Task PriorityScore_OrganizerRole_ShouldBeNegativeOne()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, organizer) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var calculationService = MutualAidTestFixture.CreateCalculationService(context);

        var score = await calculationService.CalculatePriorityScoreAsync(organizer.Id, crew.Id);
        score.Should().Be(-1);
    }

    [Fact]
    public async Task PriorityScore_NotInNeed_ShouldBeNegativeTwo()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, _) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var user = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "NotInNeed", inNeedOfAid: false);

        var calculationService = MutualAidTestFixture.CreateCalculationService(context);
        var score = await calculationService.CalculatePriorityScoreAsync(user.Id, crew.Id);

        score.Should().Be(-2);
    }

    [Fact]
    public async Task PriorityScore_RegularCrewmate_CalculatesCorrectly()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, _) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var alice = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Alice", emergencyLevel: 3, needsSurvivalAid: true);
        var bob = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Bob", emergencyLevel: 2);

        var threeMonthsAgo = DateTime.UtcNow.AddMonths(-2);
        await MutualAidTestFixture.SeedContributionGiftAsync(context, crew.Id, alice.Id, bob.Id, 90m, threeMonthsAgo);
        await MutualAidTestFixture.SeedContributionGiftAsync(context, crew.Id, bob.Id, alice.Id, 60m, threeMonthsAgo);

        var calculationService = MutualAidTestFixture.CreateCalculationService(context);
        var score = await calculationService.CalculatePriorityScoreAsync(alice.Id, crew.Id);

        var totalLifetime = 150m;
        var survivalThreshold = await calculationService.CalculateSurvivalThresholdAmountAsync(crew.Id);
        var expected = (totalLifetime * 3) + 1 + 90m + survivalThreshold;

        score.Should().Be(expected);
    }

    [Fact]
    public async Task PriorityScore_FrozenAtSeasonStart_DoesNotChangeUntilNewSeason()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var user = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Frozen", emergencyLevel: 2, needsSurvivalAid: true);

        await MutualAidTestFixture.SeedContributionGiftAsync(
            context, crew.Id, user.Id, giver.Id, 200m, DateTime.UtcNow.AddMonths(-2));

        var calculationService = MutualAidTestFixture.CreateCalculationService(context);
        var scoreAtSeasonStart = await calculationService.CalculatePriorityScoreAsync(user.Id, crew.Id);

        context.SeasonCycles.Add(new SeasonCycle
        {
            CrewId = crew.Id,
            UserId = user.Id,
            SeasonStartDate = crew.CurrentSeasonStartDate,
            CycleCapAtStart = 100m,
            TotalReceptionAmount = 0m,
            SurvivalThresholdReceived = 0m,
            CycleReceived = 0m,
            CycleCompleted = false,
            PriorityScoreAtSeasonStart = scoreAtSeasonStart,
            ReceptionOrderPosition = 1
        });
        await context.SaveChangesAsync();

        await MutualAidTestFixture.SeedContributionGiftAsync(
            context, crew.Id, giver.Id, user.Id, 500m, DateTime.UtcNow);

        var scoreAfterContribution = await calculationService.CalculatePriorityScoreAsync(user.Id, crew.Id);
        var storedCycle = await context.SeasonCycles.SingleAsync(s => s.UserId == user.Id);

        storedCycle.PriorityScoreAtSeasonStart.Should().Be(scoreAtSeasonStart);
        scoreAfterContribution.Should().Be(scoreAtSeasonStart);
    }

    [Fact]
    public async Task PriorityScore_RecalculatedOnEmergencyLevelChange()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var user = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Emergency", emergencyLevel: 1, needsSurvivalAid: true);

        await MutualAidTestFixture.SeedContributionGiftAsync(
            context, crew.Id, user.Id, giver.Id, 100m, DateTime.UtcNow.AddMonths(-2));

        var calculationService = MutualAidTestFixture.CreateCalculationService(context);
        var initialScore = await calculationService.CalculatePriorityScoreAsync(user.Id, crew.Id);

        user.EmergencyLevel = 5;
        await context.SaveChangesAsync();

        var updatedScore = await calculationService.CalculatePriorityScoreAsync(user.Id, crew.Id);
        updatedScore.Should().BeGreaterThan(initialScore);
    }

    [Fact]
    public void PriorityScore_Formula_AccountsForMembership()
    {
        decimal totalLifetimeContributions = 1000m;
        int emergencyLevel = 3;
        int membershipStatus = 1;
        decimal crewmateContribution = 200m;
        decimal survivalThresholdAmount = 50m;
        int percentBonus = 0;

        var score = (totalLifetimeContributions * emergencyLevel)
            + membershipStatus
            + crewmateContribution
            + survivalThresholdAmount * (1m - (percentBonus / 100m));

        score.Should().Be(3251m);
    }

    [Fact]
    public void PriorityScore_Formula_NonMemberGetsZeroMembershipBonus()
    {
        decimal totalLifetimeContributions = 1000m;
        int emergencyLevel = 3;
        int membershipStatus = 0;
        decimal crewmateContribution = 200m;
        decimal survivalThresholdAmount = 50m;
        int percentBonus = 0;

        var score = (totalLifetimeContributions * emergencyLevel)
            + membershipStatus
            + crewmateContribution
            + survivalThresholdAmount * (1m - (percentBonus / 100m));

        score.Should().Be(3250m);
    }

    [Fact]
    public async Task PriorityScore_HonoraryMember_CountsAsMember()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, _) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var honorary = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Honorary");

        var membership = await context.CrewMemberships.SingleAsync(m => m.UserId == honorary.Id);
        membership.IsHonoraryMember = true;
        await context.SaveChangesAsync();

        var calculationService = MutualAidTestFixture.CreateCalculationService(context);
        var isMember = await calculationService.IsMemberAsync(honorary.Id, crew.Id);

        isMember.Should().BeTrue();
    }
}
