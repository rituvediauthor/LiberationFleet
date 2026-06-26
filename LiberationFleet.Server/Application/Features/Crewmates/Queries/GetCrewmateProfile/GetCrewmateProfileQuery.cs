using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Queries.GetCrewmateProfile;

public record GetCrewmateProfileQuery(int UserId) : IRequest<CrewmateProfileResponse>;

public class GetCrewmateProfileQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    IGiftRepository giftRepository,
    IMutualAidRepository mutualAidRepository,
    IMutualAidService mutualAidService,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository) : IRequestHandler<GetCrewmateProfileQuery, CrewmateProfileResponse>
{
    public async Task<CrewmateProfileResponse> Handle(GetCrewmateProfileQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewmateProfileResponse { Success = false, Message = "Unauthorized." };
        }

        var viewerId = currentUser.UserId.Value;
        var viewerMembership = await membershipRepository.GetActiveMembershipAsync(viewerId, cancellationToken);
        if (viewerMembership is null)
        {
            return new CrewmateProfileResponse { Success = false, Message = "You are not in a crew." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(request.UserId, viewerMembership.CrewId, cancellationToken))
        {
            return new CrewmateProfileResponse { Success = false, Message = "Crewmate not found." };
        }

        var crewmate = await userRepository.GetByIdWithProfileAsync(request.UserId, cancellationToken);
        if (crewmate is null)
        {
            return new CrewmateProfileResponse { Success = false, Message = "Crewmate not found." };
        }

        var targetMembership = await membershipRepository.GetActiveMembershipAsync(request.UserId, cancellationToken);
        if (targetMembership is null || targetMembership.CrewId != viewerMembership.CrewId)
        {
            return new CrewmateProfileResponse { Success = false, Message = "Crewmate not found." };
        }

        var seasonStart = viewerMembership.Crew?.CurrentSeasonStartDate;

        var giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
            request.UserId,
            viewerMembership.CrewId,
            seasonStart,
            cancellationToken);

        var isFinancialMember = await mutualAidService.IsFinancialMemberAsync(
            request.UserId,
            viewerMembership.CrewId,
            targetMembership,
            cancellationToken);

        var priorityScore = await mutualAidService.GetPriorityScoreForUserAsync(
            request.UserId,
            viewerMembership.CrewId,
            cancellationToken,
            excludeActiveSeasonContributions: targetMembership.IsInSeason);

        var unsatisfiedThresholds = await mutualAidRepository.GetUnsatisfiedThresholdsAsync(
            viewerMembership.CrewId,
            cancellationToken);
        var isSurvivalRecipient = unsatisfiedThresholds.Any(t => t.UserId == request.UserId);

        var friendship = await friendshipRepository.GetBetweenUsersAsync(viewerId, request.UserId, cancellationToken);
        var viewerBlockedTarget = await blockRepository.IsBlockedAsync(viewerId, request.UserId, cancellationToken);
        var targetBlockedViewer = await blockRepository.IsBlockedAsync(request.UserId, viewerId, cancellationToken);

        return new CrewmateProfileResponse
        {
            Success = true,
            Message = "Crewmate profile loaded.",
            Profile = CrewmateMapper.MapProfile(
                crewmate,
                targetMembership,
                giftStats,
                isFinancialMember,
                priorityScore,
                isSurvivalRecipient,
                CrewmateMapper.MapFriendshipState(
                    viewerId,
                    request.UserId,
                    friendship,
                    viewerBlockedTarget,
                    targetBlockedViewer),
                viewerId == request.UserId)
        };
    }
}
