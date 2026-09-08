using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;

namespace LiberationFleet.Server.Tests.Application.Services;

public class MutualAidCalculationServiceTests
{
    private static Crew CreateCrew() => new()
    {
        MemberCycleCapMode = CycleCapMode.CapacityBased,
        MemberCycleCapMultiplier = 2m,
        NonMemberCycleCapMode = CycleCapMode.CapacityBased,
        NonMemberCycleCapMultiplier = 0.5m
    };

    [Fact]
    public void GetMemberCycleCap_UsesCapacityMultiplierByDefault()
    {
        MutualAidCalculationService.GetMemberCycleCap(CreateCrew(), 300m).Should().Be(600m);
    }

    [Fact]
    public void GetNonMemberCycleCap_UsesCapacityMultiplierByDefault()
    {
        MutualAidCalculationService.GetNonMemberCycleCap(CreateCrew(), 400m).Should().Be(200m);
    }

    [Fact]
    public void GetMemberCycleCap_UsesFixedAmountWhenConfigured()
    {
        var crew = CreateCrew();
        crew.MemberCycleCapMode = CycleCapMode.Fixed;
        crew.MemberCycleCapFixedAmount = 500m;

        MutualAidCalculationService.GetMemberCycleCap(crew, 300m).Should().Be(500m);
    }

    [Fact]
    public void GetSurvivalThresholdAmount_DividesHalfAmongRecipients()
    {
        MutualAidCalculationService.GetSurvivalThresholdAmount(300m, 2).Should().Be(75m);
    }

    [Fact]
    public void GetSurvivalThresholdAmount_RoundsUpToWholeDollar()
    {
        // 100 / 2 / 3 = 16.666... → $17
        MutualAidCalculationService.GetSurvivalThresholdAmount(100m, 3).Should().Be(17m);
    }

    [Fact]
    public void GetMemberCycleCap_RoundsUpToWholeDollar()
    {
        var crew = CreateCrew();
        crew.MemberCycleCapMultiplier = 0.5m;
        MutualAidCalculationService.GetMemberCycleCap(crew, 101m).Should().Be(51m);
    }

    [Fact]
    public void CeilingToWholeDollar_RoundsPositiveAmountsUp()
    {
        MutualAidCalculationService.CeilingToWholeDollar(0m).Should().Be(0m);
        MutualAidCalculationService.CeilingToWholeDollar(10m).Should().Be(10m);
        MutualAidCalculationService.CeilingToWholeDollar(10.01m).Should().Be(11m);
        MutualAidCalculationService.CeilingToWholeDollar(-5m).Should().Be(0m);
    }

    [Fact]
    public void GetSurvivalThresholdAmount_WhenNoRecipients_ReturnsZero()
    {
        MutualAidCalculationService.GetSurvivalThresholdAmount(300m, 0).Should().Be(0m);
    }

    [Fact]
    public void SurvivalThreshold_IsAtMostOneQuarterOfCycleCap_WhenOneRecipient()
    {
        const decimal total = 300m;
        var crew = CreateCrew();
        var cycleCap = MutualAidCalculationService.GetMemberCycleCap(crew, total);
        var survival = MutualAidCalculationService.GetSurvivalThresholdAmount(total, 1);

        survival.Should().Be(cycleCap / 4m);
    }

    [Fact]
    public void GetTotalMonthlyContributions_SumsEstimatedContributions()
    {
        MutualAidCalculationService.GetTotalMonthlyContributions([100m, 150m, 50m]).Should().Be(300m);
    }

    [Fact]
    public void GetEffectiveMemberCycleCap_UsesCurrentCapWhenLowerThanSeasonStart()
    {
        MutualAidCalculationService.GetEffectiveMemberCycleCap(600m, 500m).Should().Be(500m);
    }

    [Fact]
    public void GetEffectiveMemberCycleCap_UsesSeasonStartCapWhenCurrentCapIsHigher()
    {
        MutualAidCalculationService.GetEffectiveMemberCycleCap(600m, 700m).Should().Be(600m);
    }

    [Fact]
    public void GetEffectiveNonMemberCycleCap_UsesCurrentCapWhenLowerThanSeasonStart()
    {
        MutualAidCalculationService.GetEffectiveNonMemberCycleCap(400m, 350m).Should().Be(350m);
    }

    [Fact]
    public void GetEffectiveGivingSeasonJoinMonthStart_WhenFewerThan15DaysLeft_RoundsUpToNextMonth()
    {
        var joined = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);
        MutualAidCalculationService.GetEffectiveGivingSeasonJoinMonthStart(joined)
            .Should().Be(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void GetEffectiveGivingSeasonJoinMonthStart_When15OrMoreDaysLeft_UsesJoinMonth()
    {
        var joined = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);
        MutualAidCalculationService.GetEffectiveGivingSeasonJoinMonthStart(joined)
            .Should().Be(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void GetCalendarMonthContribution_UsesActualWhenFinancialGiftsExist()
    {
        MutualAidCalculationService.GetCalendarMonthContribution(
            40m,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            estimatedMonthlyContribution: 100m).Should().Be(40m);
    }

    [Fact]
    public void GetCalendarMonthContribution_UsesEstimateForEmptyMonthBeforeJoin()
    {
        MutualAidCalculationService.GetCalendarMonthContribution(
            0m,
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            estimatedMonthlyContribution: 100m).Should().Be(100m);
    }

    [Fact]
    public void GetCalendarMonthContribution_UsesZeroForEmptyMonthAfterJoin()
    {
        MutualAidCalculationService.GetCalendarMonthContribution(
            0m,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            estimatedMonthlyContribution: 100m).Should().Be(0m);
    }

    [Fact]
    public void GetCalendarMonthContribution_UsesEstimateWhenNeverJoinedGivingSeason()
    {
        MutualAidCalculationService.GetCalendarMonthContribution(
            0m,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            effectiveJoinMonthStartUtc: null,
            estimatedMonthlyContribution: 75m).Should().Be(75m);
    }

    [Fact]
    public void AverageMonthlyGivingCapacity_AveragesThreeMonths()
    {
        MutualAidCalculationService.AverageMonthlyGivingCapacity([90m, 0m, 30m]).Should().Be(40m);
    }

    [Fact]
    public void CalculateThreeMonthContributionAverage_AppliesJoinMonthFillRules()
    {
        // Sep → completed months Jun/Jul/Aug. Joined Aug 2 with $30 in Aug → (90+90+30)/3.
        var months = MutualAidCalculationService.GetPastThreeCalendarMonths(new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));
        var byMonth = new Dictionary<(int Year, int Month), decimal> { [(2026, 8)] = 30m };
        var joined = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc);

        MutualAidCalculationService.CalculateThreeMonthContributionAverage(
            months,
            byMonth,
            joined,
            estimatedMonthlyContribution: 90m).Should().Be(70m);
    }

    [Fact]
    public void CalculateThreeMonthContributionAverage_WhenJustJoinedCurrentMonth_UsesEstimateOnly()
    {
        var months = MutualAidCalculationService.GetPastThreeCalendarMonths(new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc));
        var joined = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);

        MutualAidCalculationService.CalculateThreeMonthContributionAverage(
            months,
            new Dictionary<(int Year, int Month), decimal>(),
            joined,
            estimatedMonthlyContribution: 20m).Should().Be(20m);
    }

    [Fact]
    public void GetPastThreeCalendarMonths_ExcludesCurrentMonth()
    {
        var months = MutualAidCalculationService.GetPastThreeCalendarMonths(new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc));
        months.Should().Equal((2026, 6), (2026, 7), (2026, 8));
    }

    [Fact]
    public void CalculatePriorityScoreBreakdown_ExposesFormulaComponents()
    {
        var user = HandlerTestFixture.CreateUser();
        user.EmergencyLevel = 1;
        user.PeopleRepresentedCount = 2;
        user.DisabilityLevel = 1;
        var membership = new CrewMembership { User = user, PercentBonus = 10 };

        var breakdown = MutualAidCalculationService.CalculatePriorityScoreBreakdown(
            user,
            membership,
            isFinancialMember: true,
            crewLifetimeContributions: 100m,
            userLifetimeContributions: 50m,
            survivalThresholdAmount: 5m);

        breakdown.BaseScore.Should().Be(156m);
        breakdown.PriorityMultiplier.Should().Be(4);
        breakdown.SacrificeBonusFactor.Should().Be(1.1m);
        breakdown.Score.Should().Be(156m * 4m * 1.1m);
        breakdown.MembershipBonus.Should().Be(1m);
        breakdown.PeopleRepresentedCount.Should().Be(2);
        breakdown.DisabilityLevel.Should().Be(1);
    }

    [Fact]
    public void CalculatePriorityScore_DoesNotApplyOrganizerModifierByDefault()
    {
        var user = HandlerTestFixture.CreateUser();
        user.EmergencyLevel = 1;
        var membership = new CrewMembership { IsOrganizer = true, User = user };

        var score = MutualAidCalculationService.CalculatePriorityScore(
            user,
            membership,
            isFinancialMember: true,
            crewLifetimeContributions: 1000m,
            userLifetimeContributions: 500m,
            survivalThresholdAmount: 75m);

        // multiplier = 1 + 0 + 1 = 2
        var baseScore = (1000m * 1m) + 1m + 500m + 75m;
        score.Should().Be(baseScore * 2m);
    }

    [Fact]
    public void CalculatePriorityScore_DoesNotApplyNotInNeedModifierByDefault()
    {
        var user = HandlerTestFixture.CreateUser();
        user.InNeedOfAid = false;
        user.EmergencyLevel = 1;
        var membership = new CrewMembership { User = user };

        var score = MutualAidCalculationService.CalculatePriorityScore(
            user,
            membership,
            isFinancialMember: true,
            crewLifetimeContributions: 1000m,
            userLifetimeContributions: 500m,
            survivalThresholdAmount: 75m);

        var baseScore = (1000m * 1m) + 1m + 500m + 75m;
        score.Should().Be(baseScore * 2m);
    }

    [Fact]
    public void CalculatePriorityScore_WhenOrganizerOnly_UsesSameFormulaAsProfile()
    {
        var user = HandlerTestFixture.CreateUser();
        user.InNeedOfAid = false;
        user.EmergencyLevel = 1;
        var membership = new CrewMembership { IsOrganizer = true, User = user };

        var score = MutualAidCalculationService.CalculatePriorityScore(
            user,
            membership,
            isFinancialMember: true,
            crewLifetimeContributions: 1000m,
            userLifetimeContributions: 500m,
            survivalThresholdAmount: 75m);

        var baseScore = (1000m * 1m) + 1m + 500m + 75m;
        score.Should().Be(baseScore * 2m);
    }

    [Fact]
    public void CalculatePriorityScore_WhenNotInNeedOfAid_UsesSameFormulaAsInNeed()
    {
        var user = HandlerTestFixture.CreateUser();
        user.InNeedOfAid = false;
        user.EmergencyLevel = 1;
        var membership = new CrewMembership { User = user };

        var notInNeed = MutualAidCalculationService.CalculatePriorityScore(
            user,
            membership,
            isFinancialMember: true,
            crewLifetimeContributions: 1000m,
            userLifetimeContributions: 500m,
            survivalThresholdAmount: 75m);

        user.InNeedOfAid = true;
        var inNeed = MutualAidCalculationService.CalculatePriorityScore(
            user,
            membership,
            isFinancialMember: true,
            crewLifetimeContributions: 1000m,
            userLifetimeContributions: 500m,
            survivalThresholdAmount: 75m);

        notInNeed.Should().Be(inNeed);
    }

    [Fact]
    public void CalculatePriorityScore_IncludesMembershipBonusAndSacrificePercentBonus()
    {
        var user = HandlerTestFixture.CreateUser();
        user.EmergencyLevel = 2;
        var membership = new CrewMembership { User = user, PercentBonus = 10 };

        var score = MutualAidCalculationService.CalculatePriorityScore(
            user,
            membership,
            isFinancialMember: true,
            crewLifetimeContributions: 100m,
            userLifetimeContributions: 50m,
            survivalThresholdAmount: 80m);

        // multiplier = PeopleRepresentedCount(1) + DisabilityLevel(0) + 1 = 2
        // sacrifice factor = 1.10
        var baseScore = (100m * 2m) + 1m + 50m + 80m;
        score.Should().Be(baseScore * 2m * 1.1m);
    }

    [Fact]
    public void CalculatePriorityScore_AppliesHouseholdAndDisabilityMultiplierWithPlusOne()
    {
        var user = HandlerTestFixture.CreateUser();
        user.EmergencyLevel = 2;
        user.PeopleRepresentedCount = 3;
        user.DisabilityLevel = 2;
        var membership = new CrewMembership { User = user, PercentBonus = 10 };

        var baseScore = (100m * 2m) + 1m + 50m + 80m;
        var score = MutualAidCalculationService.CalculatePriorityScore(
            user,
            membership,
            isFinancialMember: true,
            crewLifetimeContributions: 100m,
            userLifetimeContributions: 50m,
            survivalThresholdAmount: 80m);

        score.Should().Be(baseScore * 6m * 1.1m);
    }

    [Fact]
    public void CalculatePriorityScore_WhenZeroDependentsAndDisability_UsesMultiplierOfOne()
    {
        var user = HandlerTestFixture.CreateUser();
        user.PeopleRepresentedCount = 0;
        user.DisabilityLevel = 0;
        user.EmergencyLevel = 1;
        var membership = new CrewMembership { User = user };

        var score = MutualAidCalculationService.CalculatePriorityScore(
            user,
            membership,
            isFinancialMember: false,
            crewLifetimeContributions: 10m,
            userLifetimeContributions: 5m,
            survivalThresholdAmount: 0m);

        score.Should().Be(15m);
    }

    [Fact]
    public void IsCycleSatisfied_WhenCycleReceivedMeetsCap_ReturnsTrue()
    {
        var cycle = new SeasonCycle { CycleReceived = 600m, TotalReceptionAmount = 600m };

        MutualAidCalculationService.IsCycleSatisfied(cycle, 600m).Should().BeTrue();
    }

    [Fact]
    public void IsCycleSatisfied_WhenTotalReceptionExceedsCap_ButCycleReceivedBelow_ReturnsFalse()
    {
        var cycle = new SeasonCycle { CycleReceived = 100m, TotalReceptionAmount = 700m };

        MutualAidCalculationService.IsCycleSatisfied(cycle, 600m).Should().BeFalse();
    }

    [Fact]
    public void IsCycleSatisfied_WhenBelowCap_ReturnsFalse()
    {
        var cycle = new SeasonCycle { CycleReceived = 100m, TotalReceptionAmount = 100m };

        MutualAidCalculationService.IsCycleSatisfied(cycle, 600m).Should().BeFalse();
    }

    [Fact]
    public void GetCatchUpAmount_WhenCurrentCapExceedsCompletedCap_ReturnsDifference()
    {
        var cycle = new SeasonCycle
        {
            CycleCompleted = true,
            CycleReceived = 400m,
            CycleCapAtCompletion = 400m
        };

        MutualAidCalculationService.GetCatchUpAmount(cycle, 500m).Should().Be(100m);
    }

    [Fact]
    public void GetCatchUpAmount_WhenCurrentCapAtOrBelowCompletedCap_ReturnsZero()
    {
        var cycle = new SeasonCycle
        {
            CycleCompleted = true,
            CycleReceived = 400m,
            CycleCapAtCompletion = 400m
        };

        MutualAidCalculationService.GetCatchUpAmount(cycle, 400m).Should().Be(0m);
        MutualAidCalculationService.GetCatchUpAmount(cycle, 350m).Should().Be(0m);
    }

    [Fact]
    public void IsSeasonComplete_ReturnsTrueWhenAllCyclesSatisfied()
    {
        var cycles = new[]
        {
            new SeasonCycle { CycleReceived = 600m, TotalReceptionAmount = 600m },
            new SeasonCycle { CycleReceived = 600m, TotalReceptionAmount = 700m }
        };

        MutualAidCalculationService.IsSeasonComplete(cycles, _ => 600m).Should().BeTrue();
    }

    [Fact]
    public void IsSeasonComplete_ReturnsFalseWhenAnyCycleUnsatisfied()
    {
        var cycles = new[]
        {
            new SeasonCycle { CycleReceived = 600m, TotalReceptionAmount = 600m },
            new SeasonCycle { CycleReceived = 100m, TotalReceptionAmount = 100m }
        };

        MutualAidCalculationService.IsSeasonComplete(cycles, _ => 600m).Should().BeFalse();
    }

    [Fact]
    public void GetSacrificeCountFromPercentBonus_InvertsTenPercentPerSacrifice()
    {
        MutualAidCalculationService.GetSacrificeCountFromPercentBonus(0).Should().Be(0);
        MutualAidCalculationService.GetSacrificeCountFromPercentBonus(10).Should().Be(1);
        MutualAidCalculationService.GetSacrificeCountFromPercentBonus(30).Should().Be(3);
        MutualAidCalculationService.GetSacrificePercentBonus(3).Should().Be(30);
    }
}
