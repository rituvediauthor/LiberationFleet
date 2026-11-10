using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Crewmates;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Profile;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
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
    IUserBlockRepository blockRepository,
    IProposalRepository proposalRepository,
    ContentTenureService contentTenureService,
    LibraryPriorityTierService priorityTierService) : IRequestHandler<GetCrewmateProfileQuery, CrewmateProfileResponse>
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

        var (givingSeasonBreakdown, libraryBreakdown) = await mutualAidService.GetProfilePriorityBreakdownsAsync(
            request.UserId,
            viewerMembership.CrewId,
            cancellationToken);
        var priorityScore = libraryBreakdown.Score;

        var unsatisfiedThresholds = await mutualAidRepository.GetUnsatisfiedThresholdsAsync(
            viewerMembership.CrewId,
            cancellationToken);
        var crew = viewerMembership.Crew
            ?? await mutualAidRepository.GetCrewAsync(viewerMembership.CrewId, cancellationToken)
            ?? throw new InvalidOperationException("Crew not found.");
        var isSurvivalRecipient = crew.AllowSurvivalThresholds
            && unsatisfiedThresholds.Any(t => t.UserId == request.UserId);

        var friendship = await friendshipRepository.GetBetweenUsersAsync(viewerId, request.UserId, cancellationToken);
        var viewerBlockedTarget = await blockRepository.IsBlockedAsync(viewerId, request.UserId, cancellationToken);
        var targetBlockedViewer = await blockRepository.IsBlockedAsync(request.UserId, viewerId, cancellationToken);

        var canClaimIdentity = false;
        if (targetMembership.IsPlaceholderMember
            && crewmate.IsUnclaimedPlaceholder
            && viewerId != request.UserId)
        {
            var pendingClaim = await proposalRepository.GetPendingClaimPlaceholderIdentityForPlaceholderAsync(
                viewerMembership.CrewId,
                request.UserId,
                cancellationToken);
            canClaimIdentity = pendingClaim is null;
        }

        var tenureDays = await contentTenureService.GetCrewTenureDaysAsync(
            request.UserId,
            viewerMembership.CrewId,
            cancellationToken);

        SeasonCycle? seasonCycle = null;
        IReadOnlyList<MonthlySurvivalThreshold> thresholds = [];
        AidSeasonAccountingDto? seasonAccounting = null;
        if (crew.CurrentSeasonStartDate is DateTime seasonStartDate)
        {
            seasonCycle = await mutualAidRepository.GetPrimarySeasonCycleAsync(
                viewerMembership.CrewId,
                request.UserId,
                seasonStartDate,
                cancellationToken);
            thresholds = await mutualAidRepository.GetThresholdsForUserAsync(
                viewerMembership.CrewId,
                request.UserId,
                cancellationToken);
            var seasonCap = isFinancialMember ? crew.SeasonMemberCycleCap : crew.SeasonNonMemberCycleCap;
            seasonAccounting = AidStatDraftSerializer.FromLiveState(
                seasonCycle,
                thresholds,
                targetMembership.AutoJoinSeasonOnStart,
                effectiveCycleCap: seasonCap > 0m ? seasonCap : null);
            await ApplyPendingSurvivalCreditsAsync(
                giftRepository,
                viewerMembership.CrewId,
                seasonStartDate,
                request.UserId,
                seasonAccounting,
                cancellationToken);
        }
        else
        {
            seasonAccounting = AidStatDraftSerializer.Deserialize(targetMembership.AidStatDraftJson)
                ?? AidStatDraftSerializer.FromLiveState(
                    null,
                    [],
                    targetMembership.AutoJoinSeasonOnStart);
        }

        var tierSummary = await priorityTierService.GetSummaryForUserAsync(
            request.UserId,
            viewerMembership.CrewId,
            cancellationToken);

        var pendingRoleChange = await proposalRepository.GetPendingCrewRoleChangeForTargetAsync(
            viewerMembership.CrewId,
            request.UserId,
            cancellationToken);

        return new CrewmateProfileResponse
        {
            Success = true,
            Message = "Crewmate profile loaded.",
            Profile = CrewmateMapper.MapProfile(
                crewmate,
                targetMembership,
                viewerMembership,
                crew,
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
                canSocialInteract: !viewerBlockedTarget && !targetBlockedViewer,
                viewerId == request.UserId,
                tenureDays,
                canClaimIdentity,
                seasonCycle,
                ProfileMapper.ToDto(
                    givingSeasonBreakdown,
                    ProfileMapper.GivingSeasonStatusReason(targetMembership, crewmate)),
                ProfileMapper.ToDto(
                    libraryBreakdown,
                    ProfileMapper.LibraryOfThingsStatusReason(targetMembership)),
                tierSummary.ViewerTier,
                tierSummary.AverageScore,
                seasonAccounting,
                pendingRoleChange?.ProposalId)
        };
    }

    private static async Task ApplyPendingSurvivalCreditsAsync(
        IGiftRepository giftRepository,
        int crewId,
        DateTime seasonStartDate,
        int userId,
        AidSeasonAccountingDto seasonAccounting,
        CancellationToken cancellationToken)
    {
        if (seasonAccounting.SurvivalThresholds.Count == 0)
        {
            return;
        }

        var pendingCredits = await giftRepository.GetPendingReceptionCreditsAsync(
            crewId,
            seasonStartDate,
            cancellationToken);
        var pendingByThresholdId = new Dictionary<int, decimal>();
        var pendingUnscoped = 0m;
        foreach (var credit in pendingCredits.Where(c =>
                     c.IsSurvivalThreshold && c.RecipientUserId == userId))
        {
            if (credit.MonthlySurvivalThresholdId.HasValue)
            {
                pendingByThresholdId[credit.MonthlySurvivalThresholdId.Value] =
                    pendingByThresholdId.GetValueOrDefault(credit.MonthlySurvivalThresholdId.Value)
                    + credit.Amount;
            }
            else
            {
                pendingUnscoped += credit.Amount;
            }
        }

        foreach (var row in seasonAccounting.SurvivalThresholds
                     .OrderBy(t => t.Order)
                     .ThenBy(t => t.Year)
                     .ThenBy(t => t.Month))
        {
            var pending = row.Id is int id
                ? pendingByThresholdId.GetValueOrDefault(id)
                : 0m;
            if (pendingUnscoped > 0m && row.AmountRemaining > pending)
            {
                var appliedUnscoped = Math.Min(pendingUnscoped, row.AmountRemaining - pending);
                pending += appliedUnscoped;
                pendingUnscoped -= appliedUnscoped;
            }

            row.PendingAmount = pending;
        }
    }
}
