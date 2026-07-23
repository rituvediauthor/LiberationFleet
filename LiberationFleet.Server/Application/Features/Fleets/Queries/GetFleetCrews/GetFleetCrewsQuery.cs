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

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        var fleet = await fleetRepository.GetFleetForUserAsync(userId, cancellationToken);
        if (fleet is null)
        {
            return new FleetCrewListResponse
            {
                Success = false,
                Message = membership is null
                    ? "You are not in a fleet."
                    : "Your crew is not in a fleet."
            };
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
                JoinedAt = fc.JoinedAt,
                IsOwnCrew = membership?.CrewId == fc.CrewId
            });
        }

        var noCrewCount = await fleetRepository.CountNoCrewMembersAsync(fleet.Id, cancellationToken);
        items.Add(new FleetCrewListItemDto
        {
            CrewId = 0,
            CrewName = "No-Crew",
            MemberCount = noCrewCount,
            JoinedAt = DateTime.MinValue,
            IsOwnCrew = membership is null,
            IsNoCrew = true
        });

        return new FleetCrewListResponse
        {
            Success = true,
            Message = "Fleet crews loaded.",
            Items = items
        };
    }
}
