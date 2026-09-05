using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Queries.GetSeasonProfile;

public record GetSeasonProfileQuery : IRequest<SeasonProfileResponse>;

public class GetSeasonProfileQueryHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IGiftRepository giftRepository,
    IMutualAidService mutualAidService) : IRequestHandler<GetSeasonProfileQuery, SeasonProfileResponse>
{
    public async Task<SeasonProfileResponse> Handle(GetSeasonProfileQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new SeasonProfileResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var user = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        if (user is null)
        {
            return new SeasonProfileResponse { Success = false, Message = "User not found." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new SeasonProfileResponse { Success = false, Message = "You are not in a crew." };
        }

        var crew = membership.Crew ?? await crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
        var inNeedThreshold = crew?.InNeedDefaultThreshold ?? 0m;
        var giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
            userId,
            membership.CrewId,
            crew?.CurrentSeasonStartDate,
            cancellationToken);
        var canToggleOff = CrewInNeedService.CanToggleInNeedOff(giftStats.AverageMonthlyContributions, inNeedThreshold);
        var priorityScore = await mutualAidService.GetPriorityScoreForUserAsync(
            userId,
            membership.CrewId,
            cancellationToken,
            excludeActiveSeasonContributions: membership.IsInSeason);
        var canEditEstimatedContribution = SeasonProfileAccess.CanEditEstimatedContribution(membership.GivingSeasonJoinedAt);

        return new SeasonProfileResponse
        {
            Success = true,
            Message = "Season profile loaded.",
            Profile = new SeasonProfileDto
            {
                PaymentPlatforms = user.PaymentPlatforms
                    .OrderBy(p => p.Id)
                    .Select(p => new PaymentPlatformAccountDto
                    {
                        Id = p.Id,
                        PlatformId = p.CrewPaymentPlatformId ?? 0,
                        Platform = p.CrewPaymentPlatform?.Name ?? p.PlatformName,
                        CustomPlatformName = p.CrewPaymentPlatformId is null ? p.PlatformName : null,
                        Handle = p.Handle,
                        IsPreferred = p.IsPreferred
                    })
                    .ToList(),
                InNeedOfAid = user.InNeedOfAid,
                EmergencyLevel = user.EmergencyLevel,
                PeopleRepresentedCount = user.PeopleRepresentedCount,
                DisabilityLevel = user.DisabilityLevel,
                IdentityGroups = IdentityGroupKeys.Parse(user.IdentityGroups),
                NeedsSurvivalAid = user.NeedsSurvivalAid,
                CanToggleInNeedOff = canToggleOff,
                InNeedToggleThreshold = inNeedThreshold,
                EstimatedMonthlyContribution = membership.EstimatedMonthlyContribution ?? 0m,
                CanEditEstimatedContribution = canEditEstimatedContribution,
                GivingSeasonJoinedAt = membership.GivingSeasonJoinedAt,
                PriorityScore = (int)Math.Round(priorityScore, MidpointRounding.AwayFromZero)
            }
        };
    }
}
