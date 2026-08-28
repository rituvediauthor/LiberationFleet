using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Crews;

public sealed class CrewmateRejoinProposalResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int ProposalId { get; init; }

    public static CrewmateRejoinProposalResult Succeeded(int proposalId, string message) =>
        new() { Success = true, Message = message, ProposalId = proposalId };

    public static CrewmateRejoinProposalResult Failed(string message, int proposalId = 0) =>
        new() { Success = false, Message = message, ProposalId = proposalId };
}

public class CrewmateRejoinProposalService(
    IProposalRepository proposalRepository,
    IFleetRepository fleetRepository,
    ICrewRepository crewRepository,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    NotificationService notificationService,
    ContentTenureService contentTenureService,
    IUnitOfWork unitOfWork)
{
    public async Task<CrewmateRejoinProposalResult> CreateProposalAsync(
        int crewId,
        int authorUserId,
        int targetUserId,
        string username,
        CancellationToken cancellationToken)
    {
        var crew = await crewRepository.GetByIdAsync(crewId, cancellationToken);
        if (crew is null)
        {
            return CrewmateRejoinProposalResult.Failed("Crew not found.");
        }

        var authorMembership = await membershipRepository.GetMembershipAsync(authorUserId, crewId, cancellationToken);
        if (authorMembership is null || authorMembership.IsBanned)
        {
            return CrewmateRejoinProposalResult.Failed("You are not an active member of this crew.");
        }

        var (canPropose, proposeError) = await ProposalCreationAuthorization.EnsureCrewMemberCanCreateAsync(
            crew,
            authorMembership,
            giftRepository,
            contentTenureService,
            cancellationToken);
        if (!canPropose)
        {
            return CrewmateRejoinProposalResult.Failed(
                proposeError ?? "You are not allowed to create proposals yet.");
        }

        var pendingRejoin = await proposalRepository.GetPendingCrewmateRejoinForTargetAsync(
            crewId,
            targetUserId,
            cancellationToken);
        if (pendingRejoin is not null)
        {
            return CrewmateRejoinProposalResult.Failed(
                "A rejoin proposal for this crewmate is already pending.",
                pendingRejoin.ProposalId);
        }

        var membership = await membershipRepository.GetMembershipAsync(targetUserId, crewId, cancellationToken);
        if (membership is null || !membership.IsBanned)
        {
            return CrewmateRejoinProposalResult.Failed("That crewmate is not currently kicked from the crew.");
        }

        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            CrewId = crewId,
            AuthorUserId = authorUserId,
            Kind = ProposalKind.CrewmateRejoin,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        await ProposalVotingService.ApplyTimerRulesOnCreateAsync(
            proposal, utcNow, crewRepository, fleetRepository, cancellationToken);
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);
        await proposalRepository.AddCrewmateRejoinAsync(new ProposalCrewmateRejoin
        {
            Proposal = proposal,
            TargetUserId = targetUserId,
            Username = username,
            Title = $"Allow {username} to rejoin",
            Description =
                $"Allow {username} to find and join the crew again. Approval removes the kick ban but does not restore membership automatically."
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
            $"A proposal was submitted to allow {username} to rejoin the crew.",
            ProposalRouting.StatusListUrl(proposal),
            relatedEntityId: proposal.Id,
            excludeUserId: authorUserId,
            cancellationToken: cancellationToken);

        return CrewmateRejoinProposalResult.Succeeded(proposal.Id, "Rejoin proposal submitted.");
    }

    public async Task TryApplyApprovedProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Kind != ProposalKind.CrewmateRejoin || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        var rejoin = await proposalRepository.GetCrewmateRejoinByProposalIdAsync(proposal.Id, cancellationToken);
        if (rejoin is null || rejoin.IsApplied)
        {
            return;
        }

        var membership = await membershipRepository.GetMembershipAsync(rejoin.TargetUserId, proposal.CrewId!.Value, cancellationToken);
        if (membership is not null && membership.IsBanned)
        {
            await contentTenureService.OnLeftCrewAsync(
                rejoin.TargetUserId,
                proposal.CrewId!.Value,
                cancellationToken);
            membershipRepository.Remove(membership);
        }

        rejoin.IsApplied = true;
        rejoin.Description =
            $"{rejoin.Username} may now search for and join the crew again. They must rejoin on their own.";

        await notificationService.NotifyCrewAsync(
            proposal.CrewId!.Value,
            NotificationKind.CrewmateRejoinAllowed,
            "Crewmate may rejoin",
            $"{rejoin.Username} may now search for and join the crew again.",
            ProposalRouting.StatusListUrl(proposal),
            relatedEntityId: rejoin.TargetUserId,
            cancellationToken: cancellationToken);
    }
}
