using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Crews;

public sealed class ClaimPlaceholderIdentityProposalResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int ProposalId { get; init; }

    public static ClaimPlaceholderIdentityProposalResult Succeeded(int proposalId, string message) =>
        new() { Success = true, Message = message, ProposalId = proposalId };

    public static ClaimPlaceholderIdentityProposalResult Failed(string message, int proposalId = 0) =>
        new() { Success = false, Message = message, ProposalId = proposalId };
}

public class ClaimPlaceholderIdentityProposalService(
    IProposalRepository proposalRepository,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    PlaceholderCrewmateService placeholderCrewmateService,
    NotificationService notificationService)
{
    public async Task<ClaimPlaceholderIdentityProposalResult> CreateProposalAsync(
        int crewId,
        int claimantUserId,
        int placeholderUserId,
        CancellationToken cancellationToken)
    {
        if (claimantUserId == placeholderUserId)
        {
            return ClaimPlaceholderIdentityProposalResult.Failed("Invalid identity claim.");
        }

        var placeholderUser = await userRepository.GetByIdWithProfileAsync(placeholderUserId, cancellationToken);
        if (placeholderUser is null || !placeholderUser.IsUnclaimedPlaceholder)
        {
            return ClaimPlaceholderIdentityProposalResult.Failed("That person is not an unclaimed non-member.");
        }

        var placeholderMembership = await membershipRepository.GetMembershipAsync(
            placeholderUserId,
            crewId,
            cancellationToken);
        if (placeholderMembership is null
            || placeholderMembership.IsBanned
            || !placeholderMembership.IsPlaceholderMember)
        {
            return ClaimPlaceholderIdentityProposalResult.Failed("Non-member not found in your crew.");
        }

        var claimantMembership = await membershipRepository.GetMembershipAsync(claimantUserId, crewId, cancellationToken);
        if (claimantMembership is null || claimantMembership.IsBanned)
        {
            return ClaimPlaceholderIdentityProposalResult.Failed("You are not an active member of this crew.");
        }

        var pending = await proposalRepository.GetPendingClaimPlaceholderIdentityForPlaceholderAsync(
            crewId,
            placeholderUserId,
            cancellationToken);
        if (pending is not null)
        {
            return ClaimPlaceholderIdentityProposalResult.Failed(
                "An identity claim for this non-member is already pending.",
                pending.ProposalId);
        }

        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            CrewId = crewId,
            AuthorUserId = claimantUserId,
            Kind = ProposalKind.ClaimPlaceholderIdentity,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        ProposalVotingService.ApplyTimerRulesOnCreate(proposal, utcNow);
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);
        await proposalRepository.AddClaimPlaceholderIdentityAsync(new ProposalClaimPlaceholderIdentity
        {
            Proposal = proposal,
            PlaceholderUserId = placeholderUserId,
            ClaimantUserId = claimantUserId,
            PlaceholderDisplayName = placeholderUser.Username,
            Title = $"Claim identity of {placeholderUser.Username}",
            Description =
                $"{placeholderUser.Username} was added as a non-member without an account. " +
                "Approval will transfer their reception history to the claimant's account and remove the placeholder profile."
        }, cancellationToken);

        await notificationService.NotifyCrewAsync(
            crewId,
            NotificationKind.NewProposal,
            "New proposal",
            $"A proposal was submitted to claim the identity of {placeholderUser.Username}.",
            $"/app/crew/proposals/{proposal.Id}",
            excludeUserId: claimantUserId,
            cancellationToken: cancellationToken);

        return ClaimPlaceholderIdentityProposalResult.Succeeded(
            proposal.Id,
            "Identity claim submitted for crew approval.");
    }

    public async Task TryApplyApprovedProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Kind != ProposalKind.ClaimPlaceholderIdentity || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        var claim = await proposalRepository.GetClaimPlaceholderIdentityByProposalIdAsync(proposal.Id, cancellationToken);
        if (claim is null || claim.IsApplied)
        {
            return;
        }

        var placeholderUser = await userRepository.GetByIdWithProfileAsync(claim.PlaceholderUserId, cancellationToken);
        if (placeholderUser is null || !placeholderUser.IsUnclaimedPlaceholder)
        {
            claim.IsApplied = true;
            claim.Description = "This identity claim could not be applied because the placeholder no longer exists.";
            return;
        }

        await placeholderCrewmateService.MergePlaceholderIntoClaimantAsync(
            proposal.CrewId!.Value,
            claim.PlaceholderUserId,
            claim.ClaimantUserId,
            cancellationToken);

        claim.IsApplied = true;
        claim.Description =
            $"Reception history for {claim.PlaceholderDisplayName} is now associated with the claimant's account.";

        var claimant = await userRepository.GetByIdWithProfileAsync(claim.ClaimantUserId, cancellationToken);
        await notificationService.NotifyUserAsync(
            new Notifications.Contracts.CreateNotificationRequest
            {
                UserId = claim.ClaimantUserId,
                CrewId = proposal.CrewId!.Value,
                Kind = NotificationKind.ProposalAccepted,
                Title = "Identity claim approved",
                Body = $"Your claim for {claim.PlaceholderDisplayName} was approved.",
                ActionUrl = $"/app/crew/proposals/{proposal.Id}",
                RelatedEntityId = proposal.Id
            },
            cancellationToken);

        await notificationService.NotifyCrewAsync(
            proposal.CrewId!.Value,
            NotificationKind.ProposalAccepted,
            "Identity claim approved",
            $"{claimant?.Username ?? "A crewmate"} is now linked to {claim.PlaceholderDisplayName}'s reception history.",
            $"/app/crew/proposals/{proposal.Id}",
            excludeUserId: claim.ClaimantUserId,
            cancellationToken: cancellationToken);
    }
}
