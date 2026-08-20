using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.EmergencyRequests;

public static class EmergencyRequestAccess
{
    /// <summary>
    /// True when the viewer is in the request's crew, or in another crew that shares a fleet with it.
    /// </summary>
    public static async Task<bool> CanAccessAsync(
        IFleetRepository fleetRepository,
        int viewerCrewId,
        int requestCrewId,
        CancellationToken cancellationToken = default)
    {
        if (viewerCrewId == requestCrewId)
        {
            return true;
        }

        var viewerFleet = await fleetRepository.GetFleetForCrewAsync(viewerCrewId, cancellationToken);
        if (viewerFleet is null)
        {
            return false;
        }

        return await fleetRepository.IsCrewInFleetAsync(requestCrewId, viewerFleet.Id, cancellationToken);
    }

    public static async Task<(EmergencyRequest? Request, string? Error)> GetAccessibleRequestAsync(
        IEmergencyRequestRepository emergencyRequestRepository,
        IFleetRepository fleetRepository,
        int requestId,
        int viewerCrewId,
        CancellationToken cancellationToken = default,
        bool withDetails = false)
    {
        var emergencyRequest = withDetails
            ? await emergencyRequestRepository.GetByIdWithDetailsAsync(requestId, cancellationToken)
            : await emergencyRequestRepository.GetByIdAsync(requestId, cancellationToken);

        if (emergencyRequest is null)
        {
            return (null, "Emergency request not found.");
        }

        if (!await CanAccessAsync(fleetRepository, viewerCrewId, emergencyRequest.CrewId, cancellationToken))
        {
            return (null, "Emergency request not found.");
        }

        return (emergencyRequest, null);
    }
}
