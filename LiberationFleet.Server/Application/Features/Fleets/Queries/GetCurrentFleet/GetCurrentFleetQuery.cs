using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Application.Features.Library;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetCurrentFleet;

public record GetCurrentFleetQuery : IRequest<FleetOperationResponse>;

public class GetCurrentFleetQueryHandler(
    ICurrentUserService currentUser,
    IFleetRepository fleetRepository,
    LibraryPriorityTierService priorityTierService) : IRequestHandler<GetCurrentFleetQuery, FleetOperationResponse>
{
    public async Task<FleetOperationResponse> Handle(GetCurrentFleetQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FleetOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var fleet = await fleetRepository.GetFleetForUserAsync(currentUser.UserId.Value, cancellationToken);
        if (fleet is null)
        {
            return new FleetOperationResponse { Success = false, Message = "You are not in a fleet." };
        }

        var userId = currentUser.UserId.Value;
        var crewCount = (await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken)).Count;
        var tierSnapshot = await priorityTierService.GetSnapshotForFleetAsync(fleet.Id, cancellationToken);
        var tierCounts = await priorityTierService.GetFleetTierCountsForViewerAsync(
            fleet.Id,
            userId,
            cancellationToken);
        return new FleetOperationResponse
        {
            Success = true,
            Message = "Fleet loaded.",
            Fleet = FleetMapper.MapFleet(
                fleet,
                crewCount,
                tierSnapshot?.AverageScore ?? 0m,
                tierCounts)
        };
    }
}
