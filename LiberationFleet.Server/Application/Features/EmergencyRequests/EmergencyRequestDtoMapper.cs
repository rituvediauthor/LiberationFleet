using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.EmergencyRequests;

public static class EmergencyRequestDtoMapper
{
    public static (decimal AmountReceived, decimal AmountSplitCommitted, decimal AmountUncovered, decimal AmountRemaining)
        MapAmounts(EmergencyRequest request)
    {
        var received = request.AmountReceived;
        var splitCommitted = request.AmountSplitCommitted;
        var uncovered = EmergencyRequestAccounting.GetAmountUncovered(request);
        var remaining = EmergencyRequestAccounting.GetAmountRemainingToReceive(request);
        return (received, splitCommitted, uncovered, remaining);
    }
}
