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

public class CrewJoinRequestProposalService(
    IProposalRepository proposalRepository,
    IFleetRepository fleetRepository,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    ICrewInvitationRepository invitationRepository,
    IUserRepository userRepository,
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
            ? $"{applicant.Username} accepted the crew's public rules and requested to join. If approved, they will leave their current crew and join this one. A crewmate should prepare an encryption key before approval when possible."
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

        // Applicant is not a crewmate yet and must not cast the default author approve vote.
        // Crewmates approve the request through normal voting / timer rules.

        await notificationService.NotifyCrewAsync(
            crewId,
            NotificationKind.JoinRequestFromPerson,
            "Join request",
            switchingCrews
                ? $"{applicant.Username} requested to join (will leave their current crew if approved)."
                : $"{applicant.Username} requested to join the crew.",
            ProposalRouting.StatusListUrl(proposal),
            relatedEntityId: proposal.Id,
            excludeUserId: applicantUserId,
            cancellationToken: cancellationToken);

        return CrewJoinRequestResult.Succeeded(
            proposal.Id,
            switchingCrews
                ? "Join request submitted. You will leave your current crew only if this crew approves."
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

        var activeMembership = await membershipRepository.GetActiveMembershipAsync(
            joinRequest.ApplicantUserId,
            cancellationToken);
        if (activeMembership is not null && activeMembership.CrewId == proposal.CrewId)
        {
            joinRequest.IsApplied = true;
            joinRequest.Description = $"{joinRequest.ApplicantUsername} is already a member of this crew.";
            return;
        }

        if (await membershipRepository.IsUserBannedFromCrewAsync(joinRequest.ApplicantUserId, proposal.CrewId!.Value, cancellationToken))
        {
            joinRequest.IsApplied = true;
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
            joinRequest.Description = "The crew was full when this request was approved.";
            return;
        }

        if (activeMembership is not null)
        {
            var sourceCrewId = activeMembership.CrewId;
            var applicantId = joinRequest.ApplicantUserId;
            await libraryMemberCleanupService.CleanupForDepartingMemberAsync(
                sourceCrewId,
                applicantId,
                cancellationToken);
            await mutualAidService.RemoveMemberFromSeasonAsync(sourceCrewId, applicantId, cancellationToken);
            await contentTenureService.OnLeftCrewAsync(applicantId, sourceCrewId, cancellationToken);
            await paymentPlatformPortability.DetachFromCrewAsync(applicantId, cancellationToken);
            await fleetMembershipService.RetainInFleetAsNoCrewAsync(applicantId, sourceCrewId, cancellationToken);
            membershipRepository.MarkLeft(activeMembership, DateTime.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await emptyCrewCleanupService.TryCleanupIfNoActiveMembersAsync(sourceCrewId, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

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
        joinRequest.Description = activeMembership is not null
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

        await notificationService.NotifyUserAsync(new Application.Features.Notifications.Contracts.CreateNotificationRequest
        {
            UserId = joinRequest.ApplicantUserId,
            CrewId = proposal.CrewId!.Value,
            Kind = NotificationKind.ProposalAccepted,
            Title = "Join request approved",
            Body = $"You were approved to join {crew.Name}.",
            ActionUrl = ProposalRouting.StatusListUrl(proposal),
            RelatedEntityId = proposal.Id
        }, cancellationToken);

        await notificationService.NotifyCrewAsync(
            proposal.CrewId!.Value,
            NotificationKind.NewCrewmate,
            "New crewmate",
            $"{joinRequest.ApplicantUsername} joined the crew.",
            $"/app/crew/crewmates/{joinRequest.ApplicantUserId}",
            relatedEntityId: joinRequest.ApplicantUserId,
            cancellationToken: cancellationToken);
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
