using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetCrewDetail;

public record GetFleetCrewDetailQuery(int CrewId) : IRequest<FleetCrewDetailResponse>;

public class GetFleetCrewDetailQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IFleetRepository fleetRepository) : IRequestHandler<GetFleetCrewDetailQuery, FleetCrewDetailResponse>
{
    public async Task<FleetCrewDetailResponse> Handle(GetFleetCrewDetailQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FleetCrewDetailResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return new FleetCrewDetailResponse { Success = false, Message = "You are not in a crew." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new FleetCrewDetailResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        if (!await fleetRepository.IsCrewInFleetAsync(request.CrewId, fleet.Id, cancellationToken))
        {
            return new FleetCrewDetailResponse { Success = false, Message = "That crew is not in your fleet." };
        }

        var crew = await crewRepository.GetByIdAsync(request.CrewId, cancellationToken);
        if (crew is null)
        {
            return new FleetCrewDetailResponse { Success = false, Message = "Crew not found." };
        }

        var members = await membershipRepository.GetActiveMembersByCrewIdAsync(request.CrewId, cancellationToken);
        return new FleetCrewDetailResponse
        {
            Success = true,
            Message = "Crew detail loaded.",
            CrewId = crew.Id,
            CrewName = crew.Name,
            Members = members.Select(m => new FleetCrewmateDto
            {
                UserId = m.UserId,
                Username = m.User?.Username ?? $"User {m.UserId}"
            }).ToList()
        };
    }
}
