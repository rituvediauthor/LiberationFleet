using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.EmergencyRequests;

public static class EmergencyRequestDtoMapper
{
    public static (decimal AmountReceived, decimal AmountSplitCommitted, decimal AmountUncovered, decimal AmountRemaining)
        MapAmounts(EmergencyRequest request, decimal pendingUnverifiedAmount = 0m)
    {
        var received = request.AmountReceived;
        var splitCommitted = request.AmountSplitCommitted;
        var pending = Math.Max(0m, pendingUnverifiedAmount);
        var uncovered = Math.Max(0m, EmergencyRequestAccounting.GetAmountUncovered(request) - pending);
        var remaining = Math.Max(0m, EmergencyRequestAccounting.GetAmountRemainingToReceive(request) - pending);
        return (received, splitCommitted, uncovered, remaining);
    }
}
