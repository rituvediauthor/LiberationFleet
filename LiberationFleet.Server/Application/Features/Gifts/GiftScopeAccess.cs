using LiberationFleet.Server.Application.Common.Interfaces.Persistence;

namespace LiberationFleet.Server.Application.Features.Gifts;

/// <summary>
/// Crew-owned gifts are visible to the home crew and to other crews in the same fleet.
/// </summary>
public static class GiftScopeAccess
{
    public static async Task<bool> CanAccessGiftCrewAsync(
        int viewerCrewId,
        int giftCrewId,
        IFleetRepository fleetRepository,
        CancellationToken cancellationToken = default)
    {
        if (viewerCrewId == giftCrewId)
        {
            return true;
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(viewerCrewId, cancellationToken);
        if (fleet is null)
        {
            return false;
        }

        return await fleetRepository.IsCrewInFleetAsync(giftCrewId, fleet.Id, cancellationToken);
    }
}
