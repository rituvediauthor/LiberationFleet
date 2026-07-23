using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Domain.Enums;
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

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        var fleet = await fleetRepository.GetFleetForUserAsync(userId, cancellationToken);
        if (fleet is null)
        {
            return new FleetCrewDetailResponse
            {
                Success = false,
                Message = membership is null
                    ? "You are not in a fleet."
                    : "Your crew is not in a fleet."
            };
        }

        if (request.CrewId == 0)
        {
            var noCrewMembers = await fleetRepository.GetNoCrewMembershipsAsync(fleet.Id, cancellationToken);
            return new FleetCrewDetailResponse
            {
                Success = true,
                Message = "No-Crew members loaded.",
                Crew = new FleetCrewDetailDto
                {
                    CrewId = 0,
                    CrewName = "No-Crew",
                    MemberCount = noCrewMembers.Count,
                    MaxSize = null,
                    IsOwnCrew = membership is null,
                    IsNoCrew = true,
                    CanKick = false,
                    CanJoin = false,
                    Crewmates = noCrewMembers.Select(m => new FleetCrewmateDto
                    {
                        UserId = m.UserId,
                        Username = m.User?.Username ?? $"User {m.UserId}"
                    }).ToList()
                }
            };
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
        var isOwnCrew = membership?.CrewId == crew.Id;
        var viewerIsNoCrew = membership is null;
        var canJoin = viewerIsNoCrew && (
            crew.Privacy == CrewPrivacy.Public
            || crew.Privacy == CrewPrivacy.Private
            || crew.Privacy == CrewPrivacy.FleetMembersOnly);

        return new FleetCrewDetailResponse
        {
            Success = true,
            Message = "Crew detail loaded.",
            Crew = new FleetCrewDetailDto
            {
                CrewId = crew.Id,
                CrewName = crew.Name,
                MemberCount = members.Count,
                MaxSize = crew.MaxSize,
                IsOwnCrew = isOwnCrew,
                IsNoCrew = false,
                CanKick = !isOwnCrew && membership is not null,
                CanJoin = canJoin,
                Crewmates = members.Select(m => new FleetCrewmateDto
                {
                    UserId = m.UserId,
                    Username = m.User?.Username ?? $"User {m.UserId}"
                }).ToList()
            }
        };
    }
}
