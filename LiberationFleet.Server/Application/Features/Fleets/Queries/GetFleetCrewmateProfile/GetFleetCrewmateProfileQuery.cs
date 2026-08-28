using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Application.Services;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetCrewmateProfile;

public record GetFleetCrewmateProfileQuery(int UserId) : IRequest<FleetCrewmateProfileResponse>;

public class GetFleetCrewmateProfileQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    IFleetRepository fleetRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    IMutualAidService mutualAidService,
    FleetAvatarVisibilityService fleetAvatarVisibility) : IRequestHandler<GetFleetCrewmateProfileQuery, FleetCrewmateProfileResponse>
{
    public async Task<FleetCrewmateProfileResponse> Handle(
        GetFleetCrewmateProfileQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FleetCrewmateProfileResponse { Success = false, Message = "Unauthorized." };
        }

        var viewerId = currentUser.UserId.Value;
        var fleet = await fleetRepository.GetFleetForUserAsync(viewerId, cancellationToken);
        if (fleet is null)
        {
            return new FleetCrewmateProfileResponse { Success = false, Message = "You are not in a fleet." };
        }

        if (!await fleetRepository.IsUserInFleetAsync(request.UserId, fleet.Id, cancellationToken))
        {
            return new FleetCrewmateProfileResponse { Success = false, Message = "Crewmate not found in your fleet." };
        }

        if (await blockRepository.IsBlockedAsync(viewerId, request.UserId, cancellationToken)
            || await blockRepository.IsBlockedAsync(request.UserId, viewerId, cancellationToken))
        {
            return new FleetCrewmateProfileResponse { Success = false, Message = "You cannot view this profile." };
        }

        var target = await userRepository.GetByIdWithProfileAsync(request.UserId, cancellationToken);
        if (target is null)
        {
            return new FleetCrewmateProfileResponse { Success = false, Message = "Crewmate not found." };
        }

        var targetMembership = await membershipRepository.GetActiveMembershipAsync(request.UserId, cancellationToken);
        string? avatarResourceId = null;
        int priorityScore = 0;
        int? homeCrewId = null;

        if (targetMembership is not null)
        {
            homeCrewId = targetMembership.CrewId;
            if (await fleetAvatarVisibility.CanShowAvatarAsync(fleet, targetMembership, cancellationToken))
            {
                avatarResourceId = target.AvatarResourceId;
            }

            var lotScore = await mutualAidService.GetPriorityScoreForUserAsync(
                request.UserId,
                targetMembership.CrewId,
                cancellationToken,
                assumeInNeedNonOrganizerForLot: true);
            priorityScore = (int)Math.Round(lotScore, MidpointRounding.AwayFromZero);
        }

        var friendship = await friendshipRepository.GetBetweenUsersAsync(viewerId, request.UserId, cancellationToken);
        var friendshipState = CrewmateMapper.MapFriendshipState(
            viewerId,
            request.UserId,
            friendship,
            viewerBlockedTarget: false,
            targetBlockedViewer: false);

        return new FleetCrewmateProfileResponse
        {
            Success = true,
            Message = "Fleet crewmate profile loaded.",
            Profile = new FleetCrewmateProfileDto
            {
                UserId = target.Id,
                Username = target.Username,
                AvatarResourceId = avatarResourceId,
                PaymentPlatforms = CrewmateMapper.MapPaymentPlatforms(target),
                PriorityScore = priorityScore,
                FriendshipState = friendshipState,
                CanSocialInteract = true,
                IsSelf = viewerId == request.UserId,
                HomeCrewId = homeCrewId
            }
        };
    }
}
