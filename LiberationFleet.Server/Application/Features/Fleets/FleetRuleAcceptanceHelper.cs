using System.Text.Json;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Fleets;

public static class FleetRuleAcceptanceHelper
{
    public static async Task<bool> NeedsRuleAcceptanceAsync(
        int userId,
        Fleet fleet,
        IFleetRepository fleetRepository,
        IUserFleetRuleAcceptanceRepository acceptanceRepository,
        CancellationToken cancellationToken)
    {
        var publicRules = await fleetRepository.GetPublicRulesAsync(fleet.Id, cancellationToken);
        if (publicRules.Count == 0)
        {
            return false;
        }

        var requiredRuleIds = publicRules.Select(r => r.Id).OrderBy(id => id).ToList();
        var acceptance = await acceptanceRepository.GetAsync(userId, fleet.Id, cancellationToken);
        return !HasAcceptedCurrentRules(acceptance?.AcceptedRuleIdsJson, requiredRuleIds);
    }

    public static bool HasAcceptedCurrentRules(string? acceptedJson, IReadOnlyList<int> requiredRuleIds)
    {
        if (requiredRuleIds.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(acceptedJson))
        {
            return false;
        }

        try
        {
            var accepted = JsonSerializer.Deserialize<List<int>>(acceptedJson) ?? [];
            return accepted.OrderBy(id => id).SequenceEqual(requiredRuleIds);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
