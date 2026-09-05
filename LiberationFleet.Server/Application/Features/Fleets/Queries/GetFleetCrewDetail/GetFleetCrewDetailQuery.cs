using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetCrewDetail;

public record GetFleetCrewDetailQuery(int CrewId) : IRequest<FleetCrewDetailResponse>;

public class GetFleetCrewDetailQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IFleetRepository fleetRepository,
    IUserBlockRepository blockRepository,
    FleetAvatarVisibilityService fleetAvatarVisibility) : IRequestHandler<GetFleetCrewDetailQuery, FleetCrewDetailResponse>
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

        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);

        if (request.CrewId == 0)
        {
            var noCrewMembers = await fleetRepository.GetNoCrewMembershipsAsync(fleet.Id, cancellationToken);
            var visibleNoCrew = noCrewMembers
                .Where(m => !hiddenUserIds.Contains(m.UserId))
                .ToList();
            return new FleetCrewDetailResponse
            {
                Success = true,
                Message = "No-Crew members loaded.",
                Crew = new FleetCrewDetailDto
                {
                    CrewId = 0,
                    CrewName = "No-Crew",
                    MemberCount = visibleNoCrew.Count,
                    MaxSize = null,
                    IsOwnCrew = membership is null,
                    IsNoCrew = true,
                    CanKick = false,
                    CanJoin = false,
                    Crewmates = visibleNoCrew.Select(m => new FleetCrewmateDto
                    {
                        UserId = m.UserId,
                        Username = m.User?.Username ?? $"User {m.UserId}",
                        AvatarResourceId = null
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
        var visibleMembers = members.Where(m => !hiddenUserIds.Contains(m.UserId)).ToList();
        var avatarAllowed = await fleetAvatarVisibility.GetAllowedUserIdsAsync(fleet, visibleMembers, cancellationToken);
        var isOwnCrew = membership?.CrewId == crew.Id;
        var canJoin = !isOwnCrew && (
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
                MemberCount = visibleMembers.Count,
                MaxSize = crew.MaxSize,
                IsOwnCrew = isOwnCrew,
                IsNoCrew = false,
                CanKick = !isOwnCrew && membership is not null,
                CanJoin = canJoin,
                Crewmates = visibleMembers
                    .OrderBy(m => m.User?.Username ?? string.Empty)
                    .Select(m => new FleetCrewmateDto
                    {
                        UserId = m.UserId,
                        Username = m.User?.Username ?? $"User {m.UserId}",
                        AvatarResourceId = FleetAvatarVisibilityService.Filter(
                            m.User?.AvatarResourceId,
                            m.UserId,
                            avatarAllowed)
                    }).ToList()
            }
        };
    }
}
