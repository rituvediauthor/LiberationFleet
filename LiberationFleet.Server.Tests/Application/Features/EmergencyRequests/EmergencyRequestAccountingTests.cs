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

    [Fact]
    public void ApplyQueueFundedReceipt_ConvertsSplitCommitmentIntoReceived()
    {
        var segment = new SeasonCycle { Id = 11 };
        var request = new EmergencyRequest
        {
            AmountNeeded = 100m,
            AmountReceived = 0m,
            AmountSplitCommitted = 100m,
            Status = Domain.Enums.EmergencyRequestStatus.Open,
            SplitOffers =
            [
                new EmergencySplitOffer
                {
                    Amount = 100m,
                    RequesterEmergencyCycleId = 11,
                    OffererQueueRole = Domain.Enums.EmergencyOffererQueueRole.ActiveCycle,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };

        EmergencyRequestAccounting.ApplyQueueFundedReceipt(request, segment, 50m);

        request.AmountReceived.Should().Be(50m);
        request.AmountSplitCommitted.Should().Be(50m);
        request.SplitOffers.Single().Amount.Should().Be(50m);
        request.SplitOffers.Single().IsCancelled.Should().BeFalse();
        request.Status.Should().Be(Domain.Enums.EmergencyRequestStatus.Open);
        EmergencyRequestAccounting.GetAmountRemainingToReceive(request).Should().Be(50m);
    }

    [Fact]
    public void ApplyQueueFundedReceipt_FullyFundsAndCancelsSplitOffer()
    {
        var segment = new SeasonCycle { Id = 11 };
        var request = new EmergencyRequest
        {
            AmountNeeded = 50m,
            AmountReceived = 0m,
            AmountSplitCommitted = 50m,
            Status = Domain.Enums.EmergencyRequestStatus.Open,
            SplitOffers =
            [
                new EmergencySplitOffer
                {
                    Amount = 50m,
                    RequesterEmergencyCycleId = 11,
                    OffererQueueRole = Domain.Enums.EmergencyOffererQueueRole.RunnerUp,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };

        EmergencyRequestAccounting.ApplyQueueFundedReceipt(request, segment, 50m);

        request.AmountReceived.Should().Be(50m);
        request.AmountSplitCommitted.Should().Be(0m);
        request.SplitOffers.Single().Amount.Should().Be(0m);
        request.SplitOffers.Single().IsCancelled.Should().BeTrue();
        request.Status.Should().Be(Domain.Enums.EmergencyRequestStatus.Fulfilled);
    }
}
