using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Crews;

public class CrewmateKickProposalService(
    IProposalRepository proposalRepository,
    IFleetRepository fleetRepository,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    IMutualAidService mutualAidService,
    NotificationService notificationService,
    LibraryMemberCleanupService libraryMemberCleanupService,
    EmptyCrewCleanupService emptyCrewCleanupService,
    ContentTenureService contentTenureService,
    IUnitOfWork unitOfWork)
{
    public Task<CrewmateKickProposalResult> CreateFromAnonymousCommentAsync(
        int crewId,
        int authorUserId,
        int targetUserId,
        int sourceProposalId,
        int? sourceCommentId,
        string anonymousNickname,
        string reason,
        CancellationToken cancellationToken) =>
        CreateProposalAsync(
            crewId,
            authorUserId,
            targetUserId,
            anonymousNickname,
            isAnonymousOrigin: true,
            sourceProposalId,
            sourceCommentId,
            reason,
            ProposalKind.CrewmateKick,
            cancellationToken);

    public Task<CrewmateKickProposalResult> CreateFromCrewmateProfileAsync(
        int crewId,
        int authorUserId,
        int targetUserId,
        string username,
        string reason,
        CancellationToken cancellationToken) =>
        CreateProposalAsync(
            crewId,
            authorUserId,
            targetUserId,
            username,
            isAnonymousOrigin: false,
            sourceProposalId: null,
            sourceCommentId: null,
            reason,
            ProposalKind.CrewmateKick,
            cancellationToken);

    public Task<CrewmateKickProposalResult> CreateSeasonKickFromCrewmateProfileAsync(
        int crewId,
        int authorUserId,
        int targetUserId,
        string username,
        string reason,
        CancellationToken cancellationToken) =>
        CreateProposalAsync(
            crewId,
            authorUserId,
            targetUserId,
            username,
            isAnonymousOrigin: false,
            sourceProposalId: null,
            sourceCommentId: null,
            reason,
            ProposalKind.CrewmateSeasonKick,
            cancellationToken);

    private async Task<CrewmateKickProposalResult> CreateProposalAsync(
        int crewId,
        int authorUserId,
        int targetUserId,
        string displayName,
        bool isAnonymousOrigin,
        int? sourceProposalId,
        int? sourceCommentId,
        string reason,
        ProposalKind kind,
        CancellationToken cancellationToken)
    {
        var isSeasonKick = kind == ProposalKind.CrewmateSeasonKick;
        var pendingKick = isSeasonKick
            ? await proposalRepository.GetPendingSeasonKickForTargetAsync(crewId, targetUserId, cancellationToken)
            : await proposalRepository.GetPendingCrewmateKickForTargetAsync(crewId, targetUserId, cancellationToken);
        if (pendingKick is not null)
        {
            return CrewmateKickProposalResult.Failed(
                isSeasonKick
                    ? "A season-removal proposal for this crewmate is already pending."
                    : "A kick proposal for this crewmate is already pending.",
                pendingKick.ProposalId);
        }

        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            CrewId = crewId,
            AuthorUserId = authorUserId,
            Kind = kind,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        ProposalVotingService.ApplyTimerRulesOnCreate(proposal, utcNow);
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);

        var trimmedReason = reason.Trim();
        string title;
        string description;
        string notifyBody;
        string successMessage;

        if (isSeasonKick)
        {
            title = $"Remove {displayName} from season";
            description = $"Remove {displayName} from the season (not from the crew). Reason: {trimmedReason}";
            notifyBody = $"A proposal was submitted to remove {displayName} from the season.";
            successMessage = "Season-removal proposal submitted.";
        }
        else
        {
            title = $"Kick {displayName}";
            description = isAnonymousOrigin
                ? sourceCommentId.HasValue
                    ? $"Remove {displayName} from the crew following reported abuse in a proposal discussion. Reason: {trimmedReason}"
                    : $"Remove {displayName} from the crew following a malicious anonymous proposal. Reason: {trimmedReason}"
                : $"Remove {displayName} from the crew. Reason: {trimmedReason}";
            notifyBody = $"A proposal was submitted to kick {displayName} from the crew.";
            successMessage = "Kick proposal submitted.";
        }

        await proposalRepository.AddCrewmateKickAsync(new ProposalCrewmateKick
        {
            Proposal = proposal,
            TargetUserId = targetUserId,
            SourceProposalId = sourceProposalId ?? 0,
            SourceCommentId = sourceCommentId,
            AnonymousNickname = displayName,
            Title = title,
            Description = description
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await ProposalVotingService.EnsureAuthorApproveVoteAsync(
            proposalRepository,
            proposal,
            utcNow,
            cancellationToken);
        var statusBefore = proposal.Status;
        await ProposalVotingService.RecalculateAfterAuthorVoteAsync(
            proposal,
            proposalRepository,
            fleetRepository,
            utcNow,
            cancellationToken);
        if (statusBefore != ProposalStatus.Approved && proposal.Status == ProposalStatus.Approved)
        {
            await TryApplyApprovedProposalAsync(proposal, cancellationToken);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyCrewAsync(
            crewId,
            NotificationKind.NewProposal,
            "New proposal",
            notifyBody,
            $"/app/crew/proposals/{proposal.Id}",
            relatedEntityId: proposal.Id,
            excludeUserId: authorUserId,
            cancellationToken: cancellationToken);

        return CrewmateKickProposalResult.Succeeded(proposal.Id, successMessage);
    }

    public async Task TryApplyApprovedProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Status != ProposalStatus.Approved
            || (proposal.Kind != ProposalKind.CrewmateKick && proposal.Kind != ProposalKind.CrewmateSeasonKick))
        {
            return;
        }

        var kick = await proposalRepository.GetCrewmateKickByProposalIdAsync(proposal.Id, cancellationToken);
        if (kick is null || kick.IsApplied)
        {
            return;
        }

        var isSeasonKick = proposal.Kind == ProposalKind.CrewmateSeasonKick;
        var isAnonymousOrigin = kick.SourceProposalId > 0;
        var targetUser = await userRepository.GetByIdWithProfileAsync(kick.TargetUserId, cancellationToken);
        if (targetUser is not null)
        {
            kick.RevealedUsername = targetUser.Username;
            if (isSeasonKick)
            {
                kick.Description = $"{targetUser.Username} was removed from the season.";
            }
            else
            {
                kick.Description = isAnonymousOrigin
                    ? $"{kick.AnonymousNickname} was identified as {targetUser.Username} and removed from the crew."
                    : $"{targetUser.Username} was removed from the crew.";
            }
        }

        if (isSeasonKick)
        {
            await mutualAidService.RemoveMemberFromSeasonAsync(
                proposal.CrewId!.Value,
                kick.TargetUserId,
                cancellationToken);
        }
        else
        {
            var membership = await membershipRepository.GetMembershipAsync(kick.TargetUserId, proposal.CrewId!.Value, cancellationToken);
            if (membership is not null && !membership.IsBanned)
            {
                await libraryMemberCleanupService.CleanupForDepartingMemberAsync(
                    proposal.CrewId!.Value,
                    kick.TargetUserId,
                    cancellationToken);
                await contentTenureService.OnLeftCrewAsync(
                    kick.TargetUserId,
                    proposal.CrewId!.Value,
                    cancellationToken);
                membership.IsBanned = true;
            }
        }

        kick.IsApplied = true;

        if (targetUser is not null)
        {
            string notificationTitle;
            string notificationBody;

            if (isSeasonKick)
            {
                notificationTitle = "Removed from the season";
                notificationBody = $"{targetUser.Username} was removed from the season.";
            }
            else
            {
                notificationTitle = "Crewmate kicked";
                notificationBody = isAnonymousOrigin
                    ? $"{kick.AnonymousNickname} ({targetUser.Username}) was removed from the crew."
                    : $"{targetUser.Username} was removed from the crew.";
            }

            await notificationService.NotifyCrewAsync(
                proposal.CrewId!.Value,
                NotificationKind.CrewmateKicked,
                notificationTitle,
                notificationBody,
                $"/app/crew/proposals/{proposal.Id}",
                relatedEntityId: kick.TargetUserId,
                cancellationToken: cancellationToken);
        }

        if (!isSeasonKick)
        {
            await emptyCrewCleanupService.TryCleanupIfNoActiveMembersAsync(proposal.CrewId!.Value, cancellationToken);
        }
    }
}
