using System.Text.Json;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Crews;

public sealed class CrewJoinRequestResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int ProposalId { get; init; }

    public static CrewJoinRequestResult Succeeded(int proposalId, string message) =>
        new() { Success = true, Message = message, ProposalId = proposalId };

    public static CrewJoinRequestResult Failed(string message, int proposalId = 0) =>
        new() { Success = false, Message = message, ProposalId = proposalId };
}

/// <summary>
/// SecondaryEntityId values on <see cref="NotificationKind.CrewJoinSwitchOffer"/> notifications.
/// Null/0 = pending Switch/Stay; 1 = stayed; 2 = switched.
/// </summary>
public static class CrewJoinSwitchOfferDecision
{
    public const int Stayed = 1;
    public const int Switched = 2;
}

public class CrewJoinRequestProposalService(
    IProposalRepository proposalRepository,
    IFleetRepository fleetRepository,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    ICrewInvitationRepository invitationRepository,
    IUserRepository userRepository,
    INotificationRepository notificationRepository,
    NotificationService notificationService,
    ContentTenureService contentTenureService,
    LibraryMemberCleanupService libraryMemberCleanupService,
    EmptyCrewCleanupService emptyCrewCleanupService,
    FleetMembershipService fleetMembershipService,
    IMutualAidService mutualAidService,
    UserPaymentPlatformPortabilityService paymentPlatformPortability,
    IUnitOfWork unitOfWork)
{
    public async Task<CrewJoinRequestResult> CreateJoinRequestAsync(
        int applicantUserId,
        int crewId,
        IReadOnlyList<int> acceptedRuleIds,
        CancellationToken cancellationToken)
    {
        var activeMembership = await membershipRepository.GetActiveMembershipAsync(applicantUserId, cancellationToken);
        if (activeMembership is not null && activeMembership.CrewId == crewId)
        {
            return CrewJoinRequestResult.Failed("You are already a member of this crew.");
        }

        if (await membershipRepository.IsUserBannedFromCrewAsync(applicantUserId, crewId, cancellationToken))
        {
            return CrewJoinRequestResult.Failed("You are banned from this crew.");
        }

        var crew = await crewRepository.GetByIdAsync(crewId, cancellationToken);
        if (crew is null)
        {
            return CrewJoinRequestResult.Failed("Crew not found.");
        }

        var memberCount = await crewRepository.CountMembersAsync(crewId, cancellationToken);
        if (memberCount >= crew.MaxSize)
        {
            return CrewJoinRequestResult.Failed("This crew is full.");
        }

        var existing = await proposalRepository.GetPendingJoinRequestForApplicantAndCrewAsync(
            applicantUserId,
            crewId,
            cancellationToken);
        if (existing is not null)
        {
            return CrewJoinRequestResult.Failed(
                "You already have a pending join request for this crew.",
                existing.ProposalId);
        }

        var applicant = await userRepository.GetByIdWithProfileAsync(applicantUserId, cancellationToken);
        if (applicant is null)
        {
            return CrewJoinRequestResult.Failed("User not found.");
        }

        var switchingCrews = activeMembership is not null;
        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            CrewId = crewId,
            AuthorUserId = applicantUserId,
            Kind = ProposalKind.CrewJoinRequest,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        ProposalVotingService.ApplyTimerRulesOnCreate(
            proposal,
            utcNow,
            ProposalAutoResolveSettings.From(crew));
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);

        var description = switchingCrews
            ? $"{applicant.Username} accepted the crew's public rules and requested to join. If approved while they are still in another crew, they will be asked whether to switch crews or stay. A crewmate should prepare an encryption key before approval when possible."
            : $"{applicant.Username} accepted the crew's public rules and requested to join. A crewmate should prepare an encryption key before approval when possible.";

        await proposalRepository.AddCrewJoinRequestAsync(new ProposalCrewJoinRequest
        {
            Proposal = proposal,
            ApplicantUserId = applicantUserId,
            ApplicantUsername = applicant.Username,
            AcceptedRuleIdsJson = JsonSerializer.Serialize(acceptedRuleIds.OrderBy(id => id)),
            Title = $"Allow {applicant.Username} to join",
            Description = description
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyCrewAsync(
            crewId,
            NotificationKind.JoinRequestFromPerson,
            "Join request",
            switchingCrews
                ? $"{applicant.Username} requested to join (already in a crew; they will choose whether to switch if approved)."
                : $"{applicant.Username} requested to join the crew.",
            ProposalRouting.StatusListUrl(proposal),
            relatedEntityId: proposal.Id,
            excludeUserId: applicantUserId,
            cancellationToken: cancellationToken);

        return CrewJoinRequestResult.Succeeded(
            proposal.Id,
            switchingCrews
                ? "Join request submitted. If approved while you are still in a crew, you will be asked whether to switch or stay."
                : "Join request submitted.");
    }

    public async Task TryApplyApprovedProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Kind != ProposalKind.CrewJoinRequest || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        var joinRequest = await proposalRepository.GetCrewJoinRequestByProposalIdAsync(proposal.Id, cancellationToken);
        if (joinRequest is null || joinRequest.IsApplied)
        {
            return;
        }

        if (joinRequest.ApplicantDecision == CrewJoinApplicantDecision.Stayed)
        {
            return;
        }

        var activeMembership = await membershipRepository.GetActiveMembershipAsync(
            joinRequest.ApplicantUserId,
            cancellationToken);
        if (activeMembership is not null && activeMembership.CrewId == proposal.CrewId)
        {
            joinRequest.IsApplied = true;
            joinRequest.ApplicantDecision = CrewJoinApplicantDecision.None;
            joinRequest.Description = $"{joinRequest.ApplicantUsername} is already a member of this crew.";
            return;
        }

        if (await membershipRepository.IsUserBannedFromCrewAsync(joinRequest.ApplicantUserId, proposal.CrewId!.Value, cancellationToken))
        {
            joinRequest.IsApplied = true;
            joinRequest.ApplicantDecision = CrewJoinApplicantDecision.None;
            joinRequest.Description = $"{joinRequest.ApplicantUsername} is banned from this crew.";
            return;
        }

        var crew = await crewRepository.GetByIdAsync(proposal.CrewId!.Value, cancellationToken);
        if (crew is null)
        {
            return;
        }

        var memberCount = await crewRepository.CountMembersAsync(proposal.CrewId!.Value, cancellationToken);
        if (memberCount >= crew.MaxSize)
        {
            joinRequest.IsApplied = true;
            joinRequest.ApplicantDecision = CrewJoinApplicantDecision.None;
            joinRequest.Description = "The crew was full when this request was approved.";
            return;
        }

        // Already in another crew: offer Switch/Stay instead of auto-moving.
        if (activeMembership is not null)
        {
            await OfferSwitchOrStayAsync(proposal, joinRequest, crew, cancellationToken);
            return;
        }

        await CompleteJoinAsync(
            proposal,
            joinRequest,
            crew,
            leftPreviousCrew: false,
            notifyApplicantJoined: true,
            cancellationToken);
    }

    private async Task OfferSwitchOrStayAsync(
        Proposal proposal,
        ProposalCrewJoinRequest joinRequest,
        Crew crew,
        CancellationToken cancellationToken)
    {
        joinRequest.ApplicantDecision = CrewJoinApplicantDecision.Pending;
        joinRequest.Description =
            $"{joinRequest.ApplicantUsername} was approved while already in another crew and must choose whether to switch.";
        // Persist decision before notifying so a failed push does not leave a retryable None state that re-spams.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var existingOffers = await notificationRepository.GetForUserByKindAndRelatedAsync(
            joinRequest.ApplicantUserId,
            NotificationKind.CrewJoinSwitchOffer,
            proposal.Id,
            cancellationToken);
        var hasPendingOffer = existingOffers.Any(o =>
            o.SecondaryEntityId is null or 0);
        if (hasPendingOffer)
        {
            return;
        }

        await notificationService.NotifyUserAsync(new Application.Features.Notifications.Contracts.CreateNotificationRequest
        {
            UserId = joinRequest.ApplicantUserId,
            CrewId = proposal.CrewId!.Value,
            Kind = NotificationKind.CrewJoinSwitchOffer,
            Title = "Join request approved",
            Body =
                $"Your request to join {crew.Name} was approved. Would you like to switch crews or stay in your present crew?",
            ActionUrl = "/app/notifications",
            RelatedEntityId = proposal.Id
        }, cancellationToken);
    }

    public async Task<CrewJoinRequestResult> RespondToSwitchOfferAsync(
        int applicantUserId,
        int proposalId,
        bool switchCrews,
        CancellationToken cancellationToken)
    {
        var joinRequest = await proposalRepository.GetCrewJoinRequestByProposalIdAsync(proposalId, cancellationToken);
        if (joinRequest is null)
        {
            return CrewJoinRequestResult.Failed("Join request not found.");
        }

        if (joinRequest.ApplicantUserId != applicantUserId)
        {
            return CrewJoinRequestResult.Failed("This join request is not yours.");
        }

        if (joinRequest.IsApplied || joinRequest.ApplicantDecision == CrewJoinApplicantDecision.Switched)
        {
            return CrewJoinRequestResult.Succeeded(proposalId, "You already joined that crew.");
        }

        if (joinRequest.ApplicantDecision == CrewJoinApplicantDecision.Stayed)
        {
            return CrewJoinRequestResult.Succeeded(proposalId, "You already chose to stay in your present crew.");
        }

        if (joinRequest.ApplicantDecision != CrewJoinApplicantDecision.Pending)
        {
            return CrewJoinRequestResult.Failed("This join request is not waiting for a switch decision.");
        }

        var proposal = await proposalRepository.GetByIdAsync(proposalId, cancellationToken);
        if (proposal is null || proposal.Kind != ProposalKind.CrewJoinRequest || proposal.Status != ProposalStatus.Approved)
        {
            return CrewJoinRequestResult.Failed("This join request is no longer approved.");
        }

        if (!switchCrews)
        {
            joinRequest.ApplicantDecision = CrewJoinApplicantDecision.Stayed;
            joinRequest.Description =
                $"{joinRequest.ApplicantUsername} chose to stay in their present crew after this join request was approved.";
            await ResolveSwitchOfferNotificationsAsync(
                applicantUserId,
                proposalId,
                CrewJoinSwitchOfferDecision.Stayed,
                "You chose to stay in your present crew.",
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return CrewJoinRequestResult.Succeeded(proposalId, "You stayed in your present crew.");
        }

        if (await membershipRepository.IsUserBannedFromCrewAsync(applicantUserId, proposal.CrewId!.Value, cancellationToken))
        {
            joinRequest.IsApplied = true;
            joinRequest.ApplicantDecision = CrewJoinApplicantDecision.None;
            joinRequest.Description = $"{joinRequest.ApplicantUsername} is banned from this crew.";
            await ResolveSwitchOfferNotificationsAsync(
                applicantUserId,
                proposalId,
                CrewJoinSwitchOfferDecision.Stayed,
                "You could not switch because you are banned from that crew.",
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return CrewJoinRequestResult.Failed("You are banned from that crew.");
        }

        var crew = await crewRepository.GetByIdAsync(proposal.CrewId!.Value, cancellationToken);
        if (crew is null)
        {
            return CrewJoinRequestResult.Failed("Crew not found.");
        }

        var memberCount = await crewRepository.CountMembersAsync(proposal.CrewId!.Value, cancellationToken);
        if (memberCount >= crew.MaxSize)
        {
            joinRequest.IsApplied = true;
            joinRequest.ApplicantDecision = CrewJoinApplicantDecision.None;
            joinRequest.Description = "The crew was full when the applicant tried to switch.";
            await ResolveSwitchOfferNotificationsAsync(
                applicantUserId,
                proposalId,
                CrewJoinSwitchOfferDecision.Stayed,
                $"You could not switch because {crew.Name} is full.",
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return CrewJoinRequestResult.Failed("That crew is full.");
        }

        var activeMembership = await membershipRepository.GetActiveMembershipAsync(applicantUserId, cancellationToken);
        if (activeMembership is not null && activeMembership.CrewId == proposal.CrewId)
        {
            joinRequest.IsApplied = true;
            joinRequest.ApplicantDecision = CrewJoinApplicantDecision.Switched;
            await ResolveSwitchOfferNotificationsAsync(
                applicantUserId,
                proposalId,
                CrewJoinSwitchOfferDecision.Switched,
                $"You are already in {crew.Name}.",
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return CrewJoinRequestResult.Succeeded(proposalId, "You are already in that crew.");
        }

        if (activeMembership is not null)
        {
            var sourceCrewId = activeMembership.CrewId;
            await libraryMemberCleanupService.CleanupForDepartingMemberAsync(
                sourceCrewId,
                applicantUserId,
                cancellationToken);
            await mutualAidService.RemoveMemberFromSeasonAsync(sourceCrewId, applicantUserId, cancellationToken);
            await contentTenureService.OnLeftCrewAsync(applicantUserId, sourceCrewId, cancellationToken);
            await paymentPlatformPortability.DetachFromCrewAsync(applicantUserId, cancellationToken);
            await fleetMembershipService.RetainInFleetAsNoCrewAsync(applicantUserId, sourceCrewId, cancellationToken);
            membershipRepository.MarkLeft(activeMembership, DateTime.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await emptyCrewCleanupService.TryCleanupIfNoActiveMembersAsync(sourceCrewId, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await CompleteJoinAsync(
            proposal,
            joinRequest,
            crew,
            leftPreviousCrew: activeMembership is not null,
            notifyApplicantJoined: false,
            cancellationToken);
        await ResolveSwitchOfferNotificationsAsync(
            applicantUserId,
            proposalId,
            CrewJoinSwitchOfferDecision.Switched,
            $"You switched crews and joined {crew.Name}.",
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CrewJoinRequestResult.Succeeded(proposalId, $"You joined {crew.Name}.");
    }

    private async Task CompleteJoinAsync(
        Proposal proposal,
        ProposalCrewJoinRequest joinRequest,
        Crew crew,
        bool leftPreviousCrew,
        bool notifyApplicantJoined,
        CancellationToken cancellationToken)
    {
        await membershipRepository.ReactivateOrCreateAsync(
            joinRequest.ApplicantUserId,
            proposal.CrewId!.Value,
            cancellationToken);
        await paymentPlatformPortability.RemountToCrewAsync(
            joinRequest.ApplicantUserId,
            proposal.CrewId!.Value,
            cancellationToken);

        var applicant = await userRepository.GetByIdWithProfileAsync(joinRequest.ApplicantUserId, cancellationToken);
        if (applicant is not null && !applicant.IsCrewGiftRecipient)
        {
            await contentTenureService.OnJoinedCrewAsync(
                joinRequest.ApplicantUserId,
                proposal.CrewId!.Value,
                cancellationToken);
        }

        var joinedFleet = await fleetRepository.GetFleetForCrewAsync(proposal.CrewId!.Value, cancellationToken);
        if (joinedFleet is not null)
        {
            await fleetRepository.RemoveFleetMembershipForUserAsync(
                joinRequest.ApplicantUserId,
                joinedFleet.Id,
                cancellationToken);
        }

        await proposalRepository.RejectPendingJoinRequestsForApplicantAsync(
            joinRequest.ApplicantUserId,
            proposal.Id,
            cancellationToken);

        joinRequest.IsApplied = true;
        joinRequest.ApplicantDecision = leftPreviousCrew
            ? CrewJoinApplicantDecision.Switched
            : CrewJoinApplicantDecision.None;
        joinRequest.Description = leftPreviousCrew
            ? $"{joinRequest.ApplicantUsername} left their previous crew and joined this one."
            : $"{joinRequest.ApplicantUsername} was approved and joined the crew.";

        var pendingInvitation = await invitationRepository.GetPendingAsync(
            proposal.CrewId!.Value,
            joinRequest.ApplicantUserId,
            cancellationToken);
        if (pendingInvitation is not null)
        {
            pendingInvitation.Status = CrewInvitationStatus.Accepted;
            pendingInvitation.RespondedAt = DateTime.UtcNow;
        }

        if (notifyApplicantJoined)
        {
            await notificationService.NotifyUserAsync(new Application.Features.Notifications.Contracts.CreateNotificationRequest
            {
                UserId = joinRequest.ApplicantUserId,
                CrewId = proposal.CrewId!.Value,
                Kind = NotificationKind.ProposalAccepted,
                Title = "Join request approved",
                Body = $"You were approved to join {crew.Name}.",
                ActionUrl = "/app/crew",
                RelatedEntityId = proposal.Id
            }, cancellationToken);
        }

        await notificationService.NotifyCrewAsync(
            proposal.CrewId!.Value,
            NotificationKind.NewCrewmate,
            "New crewmate",
            $"{joinRequest.ApplicantUsername} joined the crew.",
            $"/app/crew/crewmates/{joinRequest.ApplicantUserId}",
            relatedEntityId: joinRequest.ApplicantUserId,
            cancellationToken: cancellationToken);
    }

    private async Task ResolveSwitchOfferNotificationsAsync(
        int userId,
        int proposalId,
        int decisionCode,
        string resolvedBody,
        CancellationToken cancellationToken)
    {
        var offers = await notificationRepository.GetForUserByKindAndRelatedAsync(
            userId,
            NotificationKind.CrewJoinSwitchOffer,
            proposalId,
            cancellationToken);
        foreach (var offer in offers)
        {
            offer.SecondaryEntityId = decisionCode;
            offer.Body = NotificationPreview.Truncate(resolvedBody);
            offer.IsRead = true;
        }
    }

    public Task MarkKeyPreparedAsync(int crewId, int applicantUserId, CancellationToken cancellationToken) =>
        MarkKeyPreparedInternalAsync(crewId, applicantUserId, cancellationToken);

    private async Task MarkKeyPreparedInternalAsync(int crewId, int applicantUserId, CancellationToken cancellationToken)
    {
        var pending = await proposalRepository.GetPendingJoinRequestForApplicantAndCrewAsync(
            applicantUserId,
            crewId,
            cancellationToken);
        if (pending is not null)
        {
            pending.IsKeyPrepared = true;
        }
    }
}
