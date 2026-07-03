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
        NonMemberCycleCapMultiplier = 0.25m
    };

    [Fact]
    public void GetMemberCycleCap_UsesCapacityMultiplierByDefault()
    {
        MutualAidCalculationService.GetMemberCycleCap(CreateCrew(), 300m).Should().Be(600m);
    }

    [Fact]
    public void GetNonMemberCycleCap_UsesCapacityMultiplierByDefault()
    {
        MutualAidCalculationService.GetNonMemberCycleCap(CreateCrew(), 400m).Should().Be(100m);
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
    public void CalculateMonthlyGivingCapacity_UsesRecentContributionsWhenPresent()
    {
        MutualAidCalculationService.CalculateMonthlyGivingCapacity(90m, 25m).Should().Be(30m);
    }

    [Fact]
    public void CalculateMonthlyGivingCapacity_FallsBackToEstimateWhenNoContributions()
    {
        MutualAidCalculationService.CalculateMonthlyGivingCapacity(0m, 40m).Should().Be(40m);
    }

    [Fact]
    public void CalculateMonthlyGivingCapacity_ReturnsZeroWhenNoContributionsOrEstimate()
    {
        MutualAidCalculationService.CalculateMonthlyGivingCapacity(0m, null).Should().Be(0m);
    }

    [Fact]
    public void CalculatePriorityScore_ReturnsNegativeOneForOrganizer()
    {
        var user = HandlerTestFixture.CreateUser();
        var membership = new CrewMembership { IsOrganizer = true, User = user };

        MutualAidCalculationService.CalculatePriorityScore(
            user,
            membership,
            isFinancialMember: true,
            crewLifetimeContributions: 1000m,
            userLifetimeContributions: 500m,
            survivalThresholdAmount: 75m).Should().Be(-1m);
    }

    [Fact]
    public void CalculatePriorityScore_ReturnsNegativeTwoWhenNotInNeedOfAid()
    {
        var user = HandlerTestFixture.CreateUser();
        user.InNeedOfAid = false;
        var membership = new CrewMembership { User = user };

        MutualAidCalculationService.CalculatePriorityScore(
            user,
            membership,
            isFinancialMember: true,
            crewLifetimeContributions: 1000m,
            userLifetimeContributions: 500m,
            survivalThresholdAmount: 75m).Should().Be(-2m);
    }

    [Fact]
    public void CalculatePriorityScore_IncludesMembershipBonusAndPercentBonus()
    {
        var user = HandlerTestFixture.CreateUser();
        user.EmergencyLevel = 2;
        user.PercentBonus = 10;
        var membership = new CrewMembership { User = user };

        var score = MutualAidCalculationService.CalculatePriorityScore(
            user,
            membership,
            isFinancialMember: true,
            crewLifetimeContributions: 100m,
            userLifetimeContributions: 50m,
            survivalThresholdAmount: 80m);

        score.Should().Be((100m * 2m) + 1m + 50m + (80m * 0.9m));
    }

    [Fact]
    public void IsCycleSatisfied_WhenCycleReceivedMeetsCap_ReturnsTrue()
    {
        var cycle = new SeasonCycle { CycleReceived = 600m, TotalReceptionAmount = 600m };

        MutualAidCalculationService.IsCycleSatisfied(cycle, 600m).Should().BeTrue();
    }

    [Fact]
    public void IsCycleSatisfied_WhenTotalReceptionExceedsCap_ReturnsTrue()
    {
        var cycle = new SeasonCycle { CycleReceived = 100m, TotalReceptionAmount = 700m };

        MutualAidCalculationService.IsCycleSatisfied(cycle, 600m).Should().BeTrue();
    }

    [Fact]
    public void IsCycleSatisfied_WhenBelowCap_ReturnsFalse()
    {
        var cycle = new SeasonCycle { CycleReceived = 100m, TotalReceptionAmount = 100m };

        MutualAidCalculationService.IsCycleSatisfied(cycle, 600m).Should().BeFalse();
    }

    [Fact]
    public void IsSeasonComplete_ReturnsTrueWhenAllCyclesSatisfied()
    {
        var cycles = new[]
        {
            new SeasonCycle { CycleReceived = 600m, TotalReceptionAmount = 600m },
            new SeasonCycle { CycleReceived = 500m, TotalReceptionAmount = 700m }
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
}
