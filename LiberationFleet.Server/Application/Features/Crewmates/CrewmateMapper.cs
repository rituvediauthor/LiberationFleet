using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
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
            .Select(p => p.CrewPaymentPlatformId)
            .ToHashSet();

        var commonPlatforms = crewmate.PaymentPlatforms
            .Where(p => viewerPlatformIds.Contains(p.CrewPaymentPlatformId))
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
            PlatformName = selected.CrewPaymentPlatform?.Name ?? string.Empty,
            Handle = selected.Handle,
            IsSharedWithViewer = isShared
        };
    }

    public static IReadOnlyList<CrewmatePaymentPlatformDto> MapPaymentPlatforms(User user) =>
        user.PaymentPlatforms
            .OrderByDescending(p => p.IsPreferred)
            .ThenBy(p => p.Id)
            .Select(p => new CrewmatePaymentPlatformDto
            {
                PlatformId = p.CrewPaymentPlatformId,
                PlatformName = p.CrewPaymentPlatform?.Name ?? string.Empty,
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
        bool isSelf,
        bool canClaimIdentity = false)
    {
        var utcNow = DateTime.UtcNow;
        var lifetimeContributions = giftStats.LifetimeContributions;
        var canAttachFilesToCrewContent = CrewContentPermissionService.CanAttachFilesToCrewContent(
            crew,
            membership,
            lifetimeContributions,
            utcNow);
        var canCreateCrewProposals = CrewContentPermissionService.CanCreateProposals(
            crew,
            membership,
            lifetimeContributions,
            utcNow);

        return new CrewmateProfileDto
        {
            UserId = crewmate.Id,
            Username = crewmate.Username,
            Roles = CrewRoleMapper.MapRoles(membership),
            ElectedRoles = CrewRoleMapper.MapElectedRoleDtos(membership),
            PaymentPlatforms = MapPaymentPlatforms(crewmate),
            SacrificeCountLastSeason = giftStats.SacrificeCountLastSeason,
            AverageMonthlyContributions = giftStats.AverageMonthlyContributions,
            MembershipStatus = isFinancialMember,
            LifetimeContributions = lifetimeContributions,
            ReceptionThisYear = giftStats.ReceptionThisYear,
            PriorityScore = (int)Math.Round(priorityScore, MidpointRounding.AwayFromZero),
            InNeedOfAid = crewmate.InNeedOfAid,
            EmergencyLevel = crewmate.EmergencyLevel,
            PeopleRepresentedCount = crewmate.PeopleRepresentedCount,
            DisabilityLevel = crewmate.DisabilityLevel,
            IsSurvivalThresholdRecipient = isSurvivalThresholdRecipient,
            FriendshipState = friendshipState,
            IsSelf = isSelf,
            CanAttachFiles = membership.CanAttachFiles,
            CanCreateProposals = membership.CanCreateProposals,
            CanAttachFilesToCrewContent = canAttachFilesToCrewContent,
            CanCreateCrewProposals = canCreateCrewProposals,
            CanProposeAttachFilesGrant = !isSelf
                && CrewContentPermissionService.NeedsAttachFilesPermissionGrant(crew, membership, lifetimeContributions, utcNow),
            CanProposeCreateProposalsGrant = !isSelf
                && CrewContentPermissionService.NeedsCreateProposalsPermissionGrant(crew, membership, lifetimeContributions, utcNow),
            CrewmateTenureDays = CrewContentPermissionService.GetTenureDays(membership, utcNow),
            CanToggleCanAttachFiles = CrewRoleAuthorizationService.CanToggleCanAttachFiles(viewerMembership),
            CanModerateAttachments = CrewRoleAuthorizationService.CanModerateAttachments(viewerMembership),
            CanExportCrewData = CrewRoleAuthorizationService.CanExportCrewData(viewerMembership),
            IsPlaceholderMember = membership.IsPlaceholderMember,
            IsInSeason = membership.IsInSeason,
            CanClaimIdentity = canClaimIdentity
        };
    }
}
