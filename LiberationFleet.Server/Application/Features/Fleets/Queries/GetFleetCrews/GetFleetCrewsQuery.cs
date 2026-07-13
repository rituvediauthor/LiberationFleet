using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetCrews;

public record GetFleetCrewsQuery : IRequest<FleetCrewListResponse>;

public class GetFleetCrewsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IFleetRepository fleetRepository) : IRequestHandler<GetFleetCrewsQuery, FleetCrewListResponse>
{
    public async Task<FleetCrewListResponse> Handle(GetFleetCrewsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FleetCrewListResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return new FleetCrewListResponse { Success = false, Message = "You are not in a crew." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new FleetCrewListResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken);
        var items = new List<FleetCrewListItemDto>();
        foreach (var fc in fleetCrews)
        {
            var memberCount = await crewRepository.CountMembersAsync(fc.CrewId, cancellationToken);
            items.Add(new FleetCrewListItemDto
            {
                CrewId = fc.CrewId,
                CrewName = fc.Crew?.Name ?? "Unknown crew",
                MemberCount = memberCount,
                JoinedAt = fc.JoinedAt
            });
        }

        return new FleetCrewListResponse
        {
            Success = true,
            Message = "Fleet crews loaded.",
            Items = items
        };
    }
}
