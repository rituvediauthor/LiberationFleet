using FluentAssertions;
using LiberationFleet.Server.Application.Services;

namespace LiberationFleet.Server.Tests.Application.Services;

public class MutualAidCalculationServiceTests
{
    [Fact]
    public void GetMemberCycleCap_IsTwiceTotalMonthlyContributions()
    {
        MutualAidCalculationService.GetMemberCycleCap(300m).Should().Be(600m);
    }

    [Fact]
    public void GetSurvivalThresholdAmount_DividesHalfAmongRecipients()
    {
        MutualAidCalculationService.GetSurvivalThresholdAmount(300m, 2).Should().Be(75m);
    }

    [Fact]
    public void SurvivalThreshold_IsAtMostOneQuarterOfCycleCap_WhenOneRecipient()
    {
        const decimal total = 300m;
        var cycleCap = MutualAidCalculationService.GetMemberCycleCap(total);
        var survival = MutualAidCalculationService.GetSurvivalThresholdAmount(total, 1);

        survival.Should().Be(cycleCap / 4m);
    }

    [Fact]
    public void GetTotalMonthlyContributions_SumsEstimatedContributions()
    {
        MutualAidCalculationService.GetTotalMonthlyContributions([100m, 150m, 50m]).Should().Be(300m);
    }
}
