using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Application.Common;

namespace LiberationFleet.Server.Application.Features.Crews;

public class CrewmateKickProposalService(
    IProposalRepository proposalRepository,
    IFleetRepository fleetRepository,
    ICrewRepository crewRepository,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    IGiftRepository giftRepository,
    IMutualAidService mutualAidService,
    NotificationService notificationService,
    LibraryMemberCleanupService libraryMemberCleanupService,
    EmptyCrewCleanupService emptyCrewCleanupService,
    FleetMembershipService fleetMembershipService,
    ContentTenureService contentTenureService,
    UserPaymentPlatformPortabilityService paymentPlatformPortability,
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
            sourceChatMessageId: null,
            reason,
            ProposalKind.CrewmateKick,
            cancellationToken);

    public Task<CrewmateKickProposalResult> CreateFromAnonymousChatMessageAsync(
        int crewId,
        int authorUserId,
        int targetUserId,
        int sourceChatMessageId,
        string reason,
        CancellationToken cancellationToken) =>
        CreateProposalAsync(
            crewId,
            authorUserId,
            targetUserId,
            "Anonymous",
            isAnonymousOrigin: true,
            sourceProposalId: null,
            sourceCommentId: null,
            sourceChatMessageId,
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
            sourceChatMessageId: null,
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
            sourceChatMessageId: null,
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
        int? sourceChatMessageId,
        string reason,
        ProposalKind kind,
        CancellationToken cancellationToken)
    {
        var crew = await crewRepository.GetByIdAsync(crewId, cancellationToken);
        if (crew is null)
        {
            return CrewmateKickProposalResult.Failed("Crew not found.");
        }

        var authorMembership = await membershipRepository.GetMembershipAsync(authorUserId, crewId, cancellationToken);
        if (authorMembership is null || authorMembership.IsBanned)
        {
            return CrewmateKickProposalResult.Failed("You are not an active member of this crew.");
        }

        var (canPropose, proposeError) = await ProposalCreationAuthorization.EnsureCrewMemberCanCreateAsync(
            crew,
            authorMembership,
            giftRepository,
            contentTenureService,
            cancellationToken);
        if (!canPropose)
        {
            return CrewmateKickProposalResult.Failed(proposeError ?? "You are not allowed to create proposals yet.");
        }

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

        await ProposalVotingService.ApplyTimerRulesOnCreateAsync(
            proposal, utcNow, crewRepository, fleetRepository, cancellationToken);
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);

        var trimmedReason = reason.Trim();
        string title;
        string description;
        string notifyBody;
        string successMessage;

        if (isSeasonKick)
        {
            title = $"Remove {displayName} from season";
            description =
                $"Remove {displayName} from the current mutual-aid season only (they stay in the crew).\n" +
                $"Reason: {trimmedReason}";
            notifyBody = $"Proposal to remove {displayName} from the season (not from the crew).";
            successMessage = "Season-removal proposal submitted.";
        }
        else
        {
            title = $"Kick {displayName}";
            description = isAnonymousOrigin
                ? sourceChatMessageId.HasValue
                    ? $"Kick Anonymous from the crew after reported chat abuse.\nReason: {trimmedReason}"
                    : sourceCommentId.HasValue
                        ? $"Kick {displayName} from the crew after reported abuse in a proposal discussion.\nReason: {trimmedReason}"
                        : $"Kick {displayName} from the crew after a malicious anonymous proposal.\nReason: {trimmedReason}"
                : $"Kick {displayName} from the crew (membership ends; they are banned from rejoining until allowed).\nReason: {trimmedReason}";
            notifyBody = $"Proposal to kick {displayName} from the crew.";
            successMessage = "Kick proposal submitted.";
        }

        await proposalRepository.AddCrewmateKickAsync(new ProposalCrewmateKick
        {
            Proposal = proposal,
            TargetUserId = targetUserId,
            SourceProposalId = sourceProposalId ?? 0,
            SourceCommentId = sourceCommentId,
            SourceChatMessageId = sourceChatMessageId,
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
            crewRepository,
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
            ProposalRouting.StatusListUrl(proposal),
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
        var isAnonymousOrigin = kick.SourceProposalId > 0 || kick.SourceChatMessageId.HasValue;
        var targetUser = await userRepository.GetByIdWithProfileAsync(kick.TargetUserId, cancellationToken);
        if (targetUser is not null)
        {
            var targetName = UserDisplay.Name(targetUser);
            kick.RevealedUsername = targetName;
            if (isSeasonKick)
            {
                kick.Description = $"{targetName} was removed from the season.";
            }
            else
            {
                kick.Description = isAnonymousOrigin
                    ? $"{kick.AnonymousNickname} was identified as {targetName} and removed from the crew."
                    : $"{targetName} was removed from the crew.";
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
                await mutualAidService.RemoveMemberFromSeasonAsync(
                    proposal.CrewId!.Value,
                    kick.TargetUserId,
                    cancellationToken);
                await paymentPlatformPortability.DetachFromCrewAsync(kick.TargetUserId, cancellationToken);
                await fleetMembershipService.RetainInFleetAsNoCrewAsync(
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

            var targetName = UserDisplay.Name(targetUser);
            if (isSeasonKick)
            {
                notificationTitle = "Removed from the season";
                notificationBody = $"{targetName} was removed from the season.";
            }
            else
            {
                notificationTitle = "Crewmate kicked";
                notificationBody = isAnonymousOrigin
                    ? $"{kick.AnonymousNickname} ({targetName}) was removed from the crew."
                    : $"{targetName} was removed from the crew.";
            }

            await notificationService.NotifyCrewAsync(
                proposal.CrewId!.Value,
                NotificationKind.CrewmateKicked,
                notificationTitle,
                notificationBody,
                ProposalRouting.StatusListUrl(proposal),
                relatedEntityId: kick.TargetUserId,
                cancellationToken: cancellationToken);
        }

        if (!isSeasonKick)
        {
            await emptyCrewCleanupService.TryCleanupIfNoActiveMembersAsync(proposal.CrewId!.Value, cancellationToken);
        }
    }
}
