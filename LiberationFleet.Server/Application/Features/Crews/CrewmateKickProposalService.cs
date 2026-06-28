using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Crews;

public class CrewmateKickProposalService(
    IProposalRepository proposalRepository,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    NotificationService notificationService,
    EmptyCrewCleanupService emptyCrewCleanupService)
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
        CancellationToken cancellationToken)
    {
        var pendingKick = await proposalRepository.GetPendingCrewmateKickForTargetAsync(
            crewId,
            targetUserId,
            cancellationToken);
        if (pendingKick is not null)
        {
            return CrewmateKickProposalResult.Failed(
                "A kick proposal for this crewmate is already pending.",
                pendingKick.ProposalId);
        }

        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            CrewId = crewId,
            AuthorUserId = authorUserId,
            Kind = ProposalKind.CrewmateKick,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        ProposalVotingService.ApplyTimerRulesOnCreate(proposal, utcNow);
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);

        var trimmedReason = reason.Trim();
        var description = isAnonymousOrigin
            ? sourceCommentId.HasValue
                ? $"Remove {displayName} from the crew following reported abuse in a proposal discussion. Reason: {trimmedReason}"
                : $"Remove {displayName} from the crew following a malicious anonymous proposal. Reason: {trimmedReason}"
            : $"Remove {displayName} from the crew. Reason: {trimmedReason}";

        await proposalRepository.AddCrewmateKickAsync(new ProposalCrewmateKick
        {
            Proposal = proposal,
            TargetUserId = targetUserId,
            SourceProposalId = sourceProposalId ?? 0,
            SourceCommentId = sourceCommentId,
            AnonymousNickname = displayName,
            Title = $"Kick {displayName}",
            Description = description
        }, cancellationToken);

        await notificationService.NotifyCrewAsync(
            crewId,
            NotificationKind.NewProposal,
            "New proposal",
            $"A proposal was submitted to kick {displayName} from the crew.",
            $"/app/crew/proposals/{proposal.Id}",
            relatedEntityId: proposal.Id,
            excludeUserId: authorUserId,
            cancellationToken: cancellationToken);

        return CrewmateKickProposalResult.Succeeded(proposal.Id, "Kick proposal submitted.");
    }

    public async Task TryApplyApprovedProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Kind != ProposalKind.CrewmateKick || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        var kick = await proposalRepository.GetCrewmateKickByProposalIdAsync(proposal.Id, cancellationToken);
        if (kick is null || kick.IsApplied)
        {
            return;
        }

        var isAnonymousOrigin = kick.SourceProposalId > 0;
        var targetUser = await userRepository.GetByIdWithProfileAsync(kick.TargetUserId, cancellationToken);
        if (targetUser is not null)
        {
            kick.RevealedUsername = targetUser.Username;
            kick.Description = isAnonymousOrigin
                ? $"{kick.AnonymousNickname} was identified as {targetUser.Username} and removed from the crew."
                : $"{targetUser.Username} was removed from the crew.";
        }

        var membership = await membershipRepository.GetMembershipAsync(kick.TargetUserId, proposal.CrewId, cancellationToken);
        if (membership is not null && !membership.IsBanned)
        {
            membership.IsBanned = true;
        }

        kick.IsApplied = true;

        if (targetUser is not null)
        {
            var notificationBody = isAnonymousOrigin
                ? $"{kick.AnonymousNickname} ({targetUser.Username}) was removed from the crew."
                : $"{targetUser.Username} was removed from the crew.";

            await notificationService.NotifyCrewAsync(
                proposal.CrewId,
                NotificationKind.CrewmateKicked,
                "Crewmate kicked",
                notificationBody,
                $"/app/crew/proposals/{proposal.Id}",
                relatedEntityId: kick.TargetUserId,
                cancellationToken: cancellationToken);
        }

        await emptyCrewCleanupService.TryCleanupIfNoActiveMembersAsync(proposal.CrewId, cancellationToken);
    }
}
