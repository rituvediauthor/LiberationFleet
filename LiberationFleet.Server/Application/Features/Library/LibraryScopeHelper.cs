using LiberationFleet.Server.Application.Common.Interfaces.Persistence;

namespace LiberationFleet.Server.Application.Features.Library;

/// <summary>
/// When a crew is in a fleet with Library of Things enabled, browse/request
/// scopes to every crew currently in that fleet. Otherwise, only the caller's crew.
/// </summary>
public static class LibraryScopeHelper
{
    public static async Task<IReadOnlyList<int>> GetAccessibleCrewIdsAsync(
        int crewId,
        IFleetRepository fleetRepository,
        CancellationToken cancellationToken)
    {
        var fleet = await fleetRepository.GetFleetForCrewAsync(crewId, cancellationToken);
        if (fleet is null || !fleet.LibraryOfThingsEnabled)
        {
            return [crewId];
        }

        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken);
        var ids = fleetCrews.Select(fc => fc.CrewId).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [crewId];
        }

        if (!ids.Contains(crewId))
        {
            ids.Add(crewId);
        }

        return ids;
    }
}
