using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Tests.Application.Features.Crews;

public class CrewmateAidStatChangeDescriberTests
{
    [Fact]
    public void BuildDescription_ShowsBeforeAndAfterForLifetimeOverride()
    {
        var text = CrewmateAidStatChangeDescriber.BuildDescription(
            "Bob",
            [new CrewmateAidStatChangeItem { Field = CrewmateAidStatField.LifetimeContributions, NewValue = "0" }],
            new CrewmateAidStatChangeDescriber.CurrentAidSnapshot
            {
                LifetimeContributions = 304m,
                EstimatedMonthlyContribution = 100m,
                ReceptionThisYear = 50m,
                PercentBoost = 0
            });

        text.Should().Contain("Proposed mutual-aid changes for Bob:");
        text.Should().Contain("Lifetime contributions: $304.00 → $0.00");
        CrewmateAidStatChangeDescriber.BuildTitle(
            "Bob",
            [new CrewmateAidStatChangeItem { Field = CrewmateAidStatField.LifetimeContributions, NewValue = "0" }])
            .Should().Be("Change lifetime contributions for Bob");
    }

    [Fact]
    public void BuildDescription_ExpandsSeasonAccountingWithThresholdDiffs()
    {
        var before = new AidSeasonAccountingDto
        {
            CycleReceived = 35m,
            CycleCap = 100m,
            HasActiveCycle = false,
            ReceptionOrder = 2,
            AutoJoinSeasonOnStart = true,
            SurvivalThresholds =
            [
                new AidSurvivalThresholdDraftDto
                {
                    Id = 10,
                    ThresholdAmount = 50m,
                    AmountRemaining = 20m,
                    Order = 3
                }
            ]
        };
        var after = new AidSeasonAccountingDto
        {
            CycleReceived = 50m,
            CycleCap = 120m,
            HasActiveCycle = true,
            ReceptionOrder = 1,
            AutoJoinSeasonOnStart = true,
            SurvivalThresholds =
            [
                new AidSurvivalThresholdDraftDto
                {
                    Id = 10,
                    ThresholdAmount = 50m,
                    AmountRemaining = 10m,
                    Order = 1
                },
                new AidSurvivalThresholdDraftDto
                {
                    ThresholdAmount = 25m,
                    AmountRemaining = 25m,
                    Order = 5
                }
            ]
        };

        var text = CrewmateAidStatChangeDescriber.BuildDescription(
            "Alice",
            [
                new CrewmateAidStatChangeItem
                {
                    Field = CrewmateAidStatField.SeasonAccounting,
                    NewValue = AidStatDraftSerializer.Serialize(after)
                }
            ],
            new CrewmateAidStatChangeDescriber.CurrentAidSnapshot { SeasonAccounting = before });

        text.Should().Contain("Season cycle:");
        text.Should().Contain("Amount already received this cycle: $35.00 → $50.00");
        text.Should().Contain("Cycle target (total cap): $100.00 → $120.00");
        text.Should().Contain("Cycle is active (receiving now): No → Yes");
        text.Should().Contain("Reception order (1 = front of queue): #2 → #1");
        text.Should().Contain("Auto-join when the next season starts: Yes (unchanged)");
        text.Should().Contain("Adjust survival threshold #10:");
        text.Should().Contain("Add survival threshold:");
        CrewmateAidStatChangeDescriber.BuildTitle(
            "Alice",
            [
                new CrewmateAidStatChangeItem
                {
                    Field = CrewmateAidStatField.SeasonAccounting,
                    NewValue = AidStatDraftSerializer.Serialize(after)
                }
            ]).Should().Be("Update season cycle & survival aid for Alice");
    }
}
