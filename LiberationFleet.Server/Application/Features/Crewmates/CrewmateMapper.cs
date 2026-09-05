using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Crewmates;

public static class CrewmateMapper
{
    public static CrewmateFriendshipStateDto MapFriendshipState(
        int viewerUserId,
        int targetUserId,
        Friendship? friendship,
        bool viewerBlockedTarget,
        bool targetBlockedViewer)
    {
        if (viewerUserId == targetUserId)
        {
            return CrewmateFriendshipStateDto.None;
        }

        if (viewerBlockedTarget)
        {
            return CrewmateFriendshipStateDto.Blocked;
        }

        if (targetBlockedViewer)
        {
            return CrewmateFriendshipStateDto.None;
        }

        if (friendship is null)
        {
            return CrewmateFriendshipStateDto.None;
        }

        if (friendship.Status == FriendshipStatus.Accepted)
        {
            return CrewmateFriendshipStateDto.Friends;
        }

        return friendship.RequesterUserId == viewerUserId
            ? CrewmateFriendshipStateDto.RequestSent
            : CrewmateFriendshipStateDto.RequestReceived;
    }

    public static CrewmatePlatformDisplayDto? MapPlatformDisplay(User viewer, User crewmate)
    {
        var viewerPlatformIds = viewer.PaymentPlatforms
            .Where(p => p.CrewPaymentPlatformId.HasValue)
            .Select(p => p.CrewPaymentPlatformId!.Value)
            .ToHashSet();

        var commonPlatforms = crewmate.PaymentPlatforms
            .Where(p => p.CrewPaymentPlatformId.HasValue
                && viewerPlatformIds.Contains(p.CrewPaymentPlatformId.Value))
            .OrderByDescending(p => p.IsPreferred)
            .ThenBy(p => p.Id)
            .ToList();

        UserPaymentPlatform? selected = commonPlatforms.FirstOrDefault();
        var isShared = selected is not null;

        if (selected is null)
        {
            selected = crewmate.PaymentPlatforms.FirstOrDefault(p => p.IsPreferred)
                ?? crewmate.PaymentPlatforms.OrderBy(p => p.Id).FirstOrDefault();
        }

        if (selected is null)
        {
            return null;
        }

        return new CrewmatePlatformDisplayDto
        {
            PlatformName = selected.CrewPaymentPlatform?.Name ?? selected.PlatformName,
            Handle = selected.Handle,
            IsSharedWithViewer = isShared
        };
    }

    public static IReadOnlyList<CrewmatePaymentPlatformDto> MapPaymentPlatforms(User user) =>
        user.PaymentPlatforms
            .Where(p => p.CrewPaymentPlatformId.HasValue)
            .OrderByDescending(p => p.IsPreferred)
            .ThenBy(p => p.Id)
            .Select(p => new CrewmatePaymentPlatformDto
            {
                PlatformId = p.CrewPaymentPlatformId!.Value,
                PlatformName = p.CrewPaymentPlatform?.Name ?? p.PlatformName,
                Handle = p.Handle,
                IsPreferred = p.IsPreferred
            })
            .ToList();

    public static CrewmateProfileDto MapProfile(
        User crewmate,
        CrewMembership membership,
        CrewMembership viewerMembership,
        Crew crew,
        CrewmateGiftStatsDto giftStats,
        bool isFinancialMember,
        decimal priorityScore,
        bool isSurvivalThresholdRecipient,
        CrewmateFriendshipStateDto friendshipState,
        bool canSocialInteract,
        bool isSelf,
        int tenureDays,
        bool canClaimIdentity = false,
        SeasonCycle? seasonCycle = null)
    {
        var lifetimeContributions = membership.LifetimeContributionOverride ?? giftStats.LifetimeContributions;
        var receptionThisYear = membership.ReceptionThisYearOverride ?? giftStats.ReceptionThisYear;
        var canAttachFilesToCrewContent = CrewContentPermissionService.CanAttachFilesToCrewContent(
            crew,
            membership,
            lifetimeContributions,
            tenureDays);
        var canCreateCrewProposals = CrewContentPermissionService.CanCreateProposals(
            crew,
            membership,
            lifetimeContributions,
            tenureDays);

        return new CrewmateProfileDto
        {
            UserId = crewmate.Id,
            Username = crewmate.Username,
            AvatarResourceId = canAttachFilesToCrewContent ? crewmate.AvatarResourceId : null,
            Roles = CrewRoleMapper.MapRoles(membership),
            ElectedRoles = CrewRoleMapper.MapElectedRoleDtos(membership),
            PaymentPlatforms = MapPaymentPlatforms(crewmate),
            SacrificeCountLastSeason = MutualAidCalculationService.GetSacrificeCountFromPercentBonus(
                membership.PercentBonus),
            SacrificeCountThisSeason = membership.EmergencySacrificesThisSeason,
            PercentBoost = membership.PercentBonus,
            AverageMonthlyContributions = giftStats.AverageMonthlyContributions,
            MembershipStatus = isFinancialMember,
            LifetimeContributions = lifetimeContributions,
            ReceptionThisYear = receptionThisYear,
            PriorityScore = (int)Math.Round(priorityScore, MidpointRounding.AwayFromZero),
            InNeedOfAid = crewmate.InNeedOfAid,
            EmergencyLevel = crewmate.EmergencyLevel,
            PeopleRepresentedCount = crewmate.PeopleRepresentedCount,
            DisabilityLevel = crewmate.DisabilityLevel,
            IdentityGroups = IdentityGroupKeys.Parse(crewmate.IdentityGroups),
            IsSurvivalThresholdRecipient = isSurvivalThresholdRecipient,
            FriendshipState = friendshipState,
            CanSocialInteract = canSocialInteract,
            IsSelf = isSelf,
            CanAttachFiles = membership.CanAttachFiles,
            CanCreateProposals = membership.CanCreateProposals,
            CanAttachFilesToCrewContent = canAttachFilesToCrewContent,
            CanCreateCrewProposals = canCreateCrewProposals,
            CanProposeAttachFilesGrant = !isSelf
                && CrewContentPermissionService.NeedsAttachFilesPermissionGrant(crew, membership, lifetimeContributions, tenureDays),
            CanProposeCreateProposalsGrant = !isSelf
                && CrewContentPermissionService.NeedsCreateProposalsPermissionGrant(crew, membership, lifetimeContributions, tenureDays),
            CrewmateTenureDays = tenureDays,
            CanToggleCanAttachFiles = CrewRoleAuthorizationService.CanToggleCanAttachFiles(viewerMembership),
            CanModerateAttachments = CrewRoleAuthorizationService.CanModerateAttachments(viewerMembership),
            CanExportCrewData = CrewRoleAuthorizationService.CanExportCrewData(viewerMembership),
            IsPlaceholderMember = membership.IsPlaceholderMember,
            IsInSeason = membership.IsInSeason,
            CanClaimIdentity = canClaimIdentity,
            CanProposeAidStatEdits = CrewRoleAuthorizationService.CanProposeCrewmateAidStatEdits(viewerMembership),
            EstimatedMonthlyContribution = membership.EstimatedMonthlyContribution,
            TotalReceptionAmount = seasonCycle?.TotalReceptionAmount,
            SurvivalThresholdReceived = seasonCycle?.SurvivalThresholdReceived,
            CycleReceived = seasonCycle?.CycleReceived,
            CycleCompleted = seasonCycle?.CycleCompleted,
            HasActiveSeasonCycle = seasonCycle is not null || crew.CurrentSeasonStartDate.HasValue
        };
    }
}