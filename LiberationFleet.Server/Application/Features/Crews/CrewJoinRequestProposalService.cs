using System.Text.Json;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
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
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IUserRepository userRepository,
    NotificationService notificationService,
    ContentTenureService contentTenureService)
{
    public async Task<CrewJoinRequestResult> CreateJoinRequestAsync(
        int applicantUserId,
        int crewId,
        IReadOnlyList<int> acceptedRuleIds,
        CancellationToken cancellationToken)
    {
        if (await membershipRepository.GetActiveMembershipAsync(applicantUserId, cancellationToken) is not null)
        {
            return CrewJoinRequestResult.Failed("You are already a member of a crew.");
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

        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            CrewId = crewId,
            AuthorUserId = applicantUserId,
            Kind = ProposalKind.CrewJoinRequest,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        ProposalVotingService.ApplyTimerRulesOnCreate(proposal, utcNow);
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);
        await proposalRepository.AddCrewJoinRequestAsync(new ProposalCrewJoinRequest
        {
            Proposal = proposal,
            ApplicantUserId = applicantUserId,
            ApplicantUsername = applicant.Username,
            AcceptedRuleIdsJson = JsonSerializer.Serialize(acceptedRuleIds.OrderBy(id => id)),
            Title = $"Allow {applicant.Username} to join",
            Description =
                $"{applicant.Username} accepted the crew's public rules and requested to join. A crewmate should prepare an encryption key before approval when possible."
        }, cancellationToken);

        await notificationService.NotifyCrewAsync(
            crewId,
            NotificationKind.JoinRequestFromPerson,
            "Join request",
            $"{applicant.Username} requested to join the crew.",
            $"/app/crew/proposals/{proposal.Id}",
            relatedEntityId: proposal.Id,
            excludeUserId: applicantUserId,
            cancellationToken: cancellationToken);

        return CrewJoinRequestResult.Succeeded(proposal.Id, "Join request submitted.");
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

        if (await membershipRepository.GetActiveMembershipAsync(joinRequest.ApplicantUserId, cancellationToken) is not null)
        {
            joinRequest.IsApplied = true;
            joinRequest.Description = $"{joinRequest.ApplicantUsername} joined another crew first.";
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

        await membershipRepository.AddAsync(new CrewMembership
        {
            UserId = joinRequest.ApplicantUserId,
            CrewId = proposal.CrewId!.Value,
            IsBanned = false,
            JoinedAt = DateTime.UtcNow
        }, cancellationToken);

        var applicant = await userRepository.GetByIdWithProfileAsync(joinRequest.ApplicantUserId, cancellationToken);
        if (applicant is not null && !applicant.IsCrewGiftRecipient)
        {
            await contentTenureService.OnJoinedCrewAsync(
                joinRequest.ApplicantUserId,
                proposal.CrewId!.Value,
                cancellationToken);
        }

        await proposalRepository.RejectPendingJoinRequestsForApplicantAsync(
            joinRequest.ApplicantUserId,
            proposal.Id,
            cancellationToken);

        joinRequest.IsApplied = true;
        joinRequest.Description = $"{joinRequest.ApplicantUsername} was approved and joined the crew.";

        await notificationService.NotifyUserAsync(new Application.Features.Notifications.Contracts.CreateNotificationRequest
        {
            UserId = joinRequest.ApplicantUserId,
            CrewId = proposal.CrewId!.Value,
            Kind = NotificationKind.ProposalAccepted,
            Title = "Join request approved",
            Body = $"You were approved to join {crew.Name}.",
            ActionUrl = "/app/crew"
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
