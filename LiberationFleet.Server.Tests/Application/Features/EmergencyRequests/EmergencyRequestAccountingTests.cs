using LiberationFleet.Server.Application.Features.EmergencyRequests;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Tests.Application.Features.EmergencyRequests;

public class EmergencyRequestAccountingTests
{
    [Fact]
    public void GetAmountUncovered_SubtractsReceivedAndSplitCommitted()
    {
        var request = new EmergencyRequest
        {
            AmountNeeded = 100m,
            AmountReceived = 30m,
            AmountSplitCommitted = 40m
        };

        EmergencyRequestAccounting.GetAmountUncovered(request).Should().Be(30m);
    }

    [Fact]
    public void OrderSplitOffersForShrink_PlacesRunnerUpBeforeActiveCycle()
    {
        var request = new EmergencyRequest
        {
            SplitOffers =
            [
                new EmergencySplitOffer
                {
                    OffererUserId = 1,
                    Amount = 60m,
                    OffererQueueRole = Domain.Enums.EmergencyOffererQueueRole.ActiveCycle,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5)
                },
                new EmergencySplitOffer
                {
                    OffererUserId = 2,
                    Amount = 40m,
                    OffererQueueRole = Domain.Enums.EmergencyOffererQueueRole.RunnerUp,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };

        var ordered = EmergencyRequestAccounting.OrderSplitOffersForShrink(request.SplitOffers).ToList();

        ordered[0].OffererUserId.Should().Be(2);
        ordered[1].OffererUserId.Should().Be(1);
    }
}
