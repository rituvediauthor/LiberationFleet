using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain;
using LiberationFleet.Server.Domain.Entities;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Commands.UpdateSeasonProfile;

public class UpdateSeasonProfileCommandHandler(
    IUserRepository userRepository,
    IGiftRepository giftRepository,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
    ICurrentUserService currentUserService,
    IMutualAidService mutualAidService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateSeasonProfileCommand, SeasonProfileResponse>
{
    public async Task<SeasonProfileResponse> Handle(UpdateSeasonProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
        {
            return new SeasonProfileResponse { Success = false, Message = "Unauthorized." };
        }

        var user = await userRepository.GetByIdWithProfileAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return new SeasonProfileResponse { Success = false, Message = "User not found." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(userId.Value, cancellationToken);
        if (membership is null)
        {
            return new SeasonProfileResponse { Success = false, Message = "You are not in a crew." };
        }

        var canEditEstimatedContribution = SeasonProfileAccess.CanEditEstimatedContribution(membership.GivingSeasonJoinedAt);
        var currentEstimatedContribution = membership.EstimatedMonthlyContribution ?? 0m;
        if (!canEditEstimatedContribution && request.EstimatedMonthlyContribution != currentEstimatedContribution)
        {
            return new SeasonProfileResponse
            {
                Success = false,
                Message = "Estimated monthly contribution can no longer be changed after three months in the giving season."
            };
        }

        var previousEmergencyLevel = user.EmergencyLevel;
        var previousInNeedOfAid = user.InNeedOfAid;
        var previousPeopleRepresentedCount = user.PeopleRepresentedCount;
        var previousDisabilityLevel = user.DisabilityLevel;

        var crew = await crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
        var inNeedThreshold = crew?.InNeedDefaultThreshold ?? 0m;
        var monthlyExclLot = await mutualAidService.GetMonthlyContributionExcludingLotAsync(
            userId.Value,
            membership.CrewId,
            cancellationToken);

        if (CrewInNeedService.IsAtOrBelowInNeedThreshold(monthlyExclLot, inNeedThreshold))
        {
            user.InNeedOfAid = true;
        }
        else
        {
            user.InNeedOfAid = request.InNeedOfAid;
        }

        user.EmergencyLevel = request.EmergencyLevel;
        user.PeopleRepresentedCount = request.PeopleRepresentedCount;
        user.DisabilityLevel = request.DisabilityLevel;
        user.IdentityGroups = IdentityGroupKeys.Serialize(request.IdentityGroups);
        user.NeedsSurvivalAid = request.NeedsSurvivalAid;

        if (canEditEstimatedContribution)
        {
            membership.EstimatedMonthlyContribution = request.EstimatedMonthlyContribution;
        }

        var paymentPlatforms = request.PaymentPlatforms
            .Where(p => !string.IsNullOrWhiteSpace(p.Handle)
                && (p.PlatformId > 0 || !string.IsNullOrWhiteSpace(p.CustomPlatformName)))
            .ToList();

        user.PaymentPlatforms.Clear();
        var preferredAssigned = false;

        foreach (var platform in paymentPlatforms)
        {
            CrewPaymentPlatform crewPlatform;
            if (!string.IsNullOrWhiteSpace(platform.CustomPlatformName))
            {
                crewPlatform = await CrewPaymentPlatformService.EnsurePlatformAsync(
                    crewPaymentPlatformRepository,
                    unitOfWork,
                    membership.CrewId,
                    platform.CustomPlatformName,
                    cancellationToken);
            }
            else
            {
                var existing = await crewPaymentPlatformRepository.GetByIdAsync(platform.PlatformId, cancellationToken);
                if (existing is not null
                    && existing.CrewId == membership.CrewId
                    && !existing.IsLibraryOfThings)
                {
                    crewPlatform = existing;
                }
                else if (!string.IsNullOrWhiteSpace(platform.Platform))
                {
                    crewPlatform = await CrewPaymentPlatformService.EnsurePlatformAsync(
                        crewPaymentPlatformRepository,
                        unitOfWork,
                        membership.CrewId,
                        platform.Platform,
                        cancellationToken);
                }
                else
                {
                    return new SeasonProfileResponse { Success = false, Message = "Invalid payment platform for your crew." };
                }
            }

            var isPreferred = platform.IsPreferred && !preferredAssigned;
            if (isPreferred)
            {
                preferredAssigned = true;
            }

            user.PaymentPlatforms.Add(new UserPaymentPlatform
            {
                CrewPaymentPlatformId = crewPlatform.Id,
                PlatformName = crewPlatform.Name,
                Handle = platform.Handle.Trim(),
                IsPreferred = isPreferred
            });
        }

        if (!preferredAssigned && user.PaymentPlatforms.Count > 0)
        {
            user.PaymentPlatforms.First().IsPreferred = true;
        }

        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await CrewInNeedService.ApplyInNeedDefaultAsync(
            userId.Value,
            userRepository,
            giftRepository,
            crewRepository,
            membershipRepository,
            unitOfWork,
            cancellationToken);

        var reloaded = await userRepository.GetByIdWithProfileAsync(userId.Value, cancellationToken);
        if (reloaded is null)
        {
            return new SeasonProfileResponse { Success = false, Message = "User not found." };
        }

        var reloadedMembership = await membershipRepository.GetActiveMembershipAsync(userId.Value, cancellationToken);
        if (reloadedMembership is null)
        {
            return new SeasonProfileResponse { Success = false, Message = "You are not in a crew." };
        }

        if (previousEmergencyLevel != reloaded.EmergencyLevel
            || previousInNeedOfAid != reloaded.InNeedOfAid
            || previousPeopleRepresentedCount != reloaded.PeopleRepresentedCount
            || previousDisabilityLevel != reloaded.DisabilityLevel)
        {
            if (previousInNeedOfAid != reloaded.InNeedOfAid)
            {
                await mutualAidService.OnInNeedOfAidChangedAsync(
                    userId.Value,
                    reloaded.InNeedOfAid,
                    cancellationToken);
            }

            await mutualAidService.OnCrewmatePriorityChangedAsync(userId.Value, cancellationToken);
        }

        var giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
            userId.Value,
            reloadedMembership.CrewId,
            reloadedMembership.Crew?.CurrentSeasonStartDate,
            cancellationToken);
        var canToggleOff = CrewInNeedService.CanToggleInNeedOff(giftStats.AverageMonthlyContributions, inNeedThreshold);
        var priorityScore = await mutualAidService.GetPriorityScoreForUserAsync(
            userId.Value,
            reloadedMembership.CrewId,
            cancellationToken,
            excludeActiveSeasonContributions: reloadedMembership.IsInSeason);

        return new SeasonProfileResponse
        {
            Success = true,
            Message = "Season profile updated.",
            Profile = new SeasonProfileDto
            {
                PaymentPlatforms = reloaded.PaymentPlatforms
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
                InNeedOfAid = reloaded.InNeedOfAid,
                EmergencyLevel = reloaded.EmergencyLevel,
                PeopleRepresentedCount = reloaded.PeopleRepresentedCount,
                DisabilityLevel = reloaded.DisabilityLevel,
                IdentityGroups = IdentityGroupKeys.Parse(reloaded.IdentityGroups),
                NeedsSurvivalAid = reloaded.NeedsSurvivalAid,
                CanToggleInNeedOff = canToggleOff,
                InNeedToggleThreshold = inNeedThreshold,
                EstimatedMonthlyContribution = reloadedMembership.EstimatedMonthlyContribution ?? 0m,
                CanEditEstimatedContribution = SeasonProfileAccess.CanEditEstimatedContribution(reloadedMembership.GivingSeasonJoinedAt),
                GivingSeasonJoinedAt = reloadedMembership.GivingSeasonJoinedAt,
                PriorityScore = (int)Math.Round(priorityScore, MidpointRounding.AwayFromZero)
            }
        };
    }
}
