using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Crews;

public sealed class CrewmatePermissionProposalResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int ProposalId { get; init; }

    public static CrewmatePermissionProposalResult Succeeded(int proposalId, string message) =>
        new() { Success = true, ProposalId = proposalId, Message = message };

    public static CrewmatePermissionProposalResult Failed(string message, int proposalId = 0) =>
        new() { Success = false, Message = message, ProposalId = proposalId };
}

public class CrewmatePermissionProposalService(
    IProposalRepository proposalRepository,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    NotificationService notificationService)
{
    public Task<CrewmatePermissionProposalResult> CreateAttachFilesGrantAsync(
        int crewId,
        int authorUserId,
        int targetUserId,
        CancellationToken cancellationToken) =>
        CreateGrantAsync(crewId, authorUserId, targetUserId, CrewmatePermissionGrantType.AttachFiles, cancellationToken);

    public Task<CrewmatePermissionProposalResult> CreateCreateProposalsGrantAsync(
        int crewId,
        int authorUserId,
        int targetUserId,
        CancellationToken cancellationToken) =>
        CreateGrantAsync(crewId, authorUserId, targetUserId, CrewmatePermissionGrantType.CreateProposals, cancellationToken);

    private async Task<CrewmatePermissionProposalResult> CreateGrantAsync(
        int crewId,
        int authorUserId,
        int targetUserId,
        CrewmatePermissionGrantType grantType,
        CancellationToken cancellationToken)
    {
        var targetUser = await userRepository.GetByIdWithProfileAsync(targetUserId, cancellationToken);
        if (targetUser is null)
        {
            return CrewmatePermissionProposalResult.Failed("Crewmate not found.");
        }

        var membership = await membershipRepository.GetMembershipAsync(targetUserId, crewId, cancellationToken);
        if (membership is null || membership.IsBanned)
        {
            return CrewmatePermissionProposalResult.Failed("Crewmate not found.");
        }

        if (membership.IsOrganizer)
        {
            return CrewmatePermissionProposalResult.Failed("Organizers already have full permissions.");
        }

        if (grantType == CrewmatePermissionGrantType.AttachFiles && membership.CanAttachFiles)
        {
            return CrewmatePermissionProposalResult.Failed("This crewmate already has file attachment permission.");
        }

        if (grantType == CrewmatePermissionGrantType.CreateProposals && membership.CanCreateProposals)
        {
            return CrewmatePermissionProposalResult.Failed("This crewmate already has proposal creation permission.");
        }

        var pending = await proposalRepository.GetPendingCrewmatePermissionGrantForTargetAsync(
            crewId,
            targetUserId,
            grantType,
            cancellationToken);
        if (pending is not null)
        {
            return CrewmatePermissionProposalResult.Failed(
                "A permission grant proposal for this crewmate is already pending.",
                pending.ProposalId);
        }

        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            CrewId = crewId,
            AuthorUserId = authorUserId,
            Kind = ProposalKind.CrewmatePermissionGrant,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        ProposalVotingService.ApplyTimerRulesOnCreate(proposal, utcNow);
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);

        var (title, description) = grantType == CrewmatePermissionGrantType.AttachFiles
            ? (
                $"Grant file attachments to {targetUser.Username}",
                $"Allow {targetUser.Username} to attach files to crew chats, forums, comments, and library offerings.")
            : (
                $"Grant proposal creation to {targetUser.Username}",
                $"Allow {targetUser.Username} to create crew proposals regardless of tenure or contribution requirements.");

        await proposalRepository.AddCrewmatePermissionGrantAsync(new ProposalCrewmatePermissionGrant
        {
            Proposal = proposal,
            TargetUserId = targetUserId,
            GrantType = grantType,
            Title = title,
            Description = description
        }, cancellationToken);

        await notificationService.NotifyCrewAsync(
            crewId,
            NotificationKind.NewProposal,
            "New proposal",
            $"A proposal was submitted to grant permissions to {targetUser.Username}.",
            $"/app/crew/proposals/{proposal.Id}",
            relatedEntityId: proposal.Id,
            excludeUserId: authorUserId,
            cancellationToken: cancellationToken);

        return CrewmatePermissionProposalResult.Succeeded(
            proposal.Id,
            "Permission grant proposal submitted.");
    }

    public async Task TryApplyApprovedProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Kind != ProposalKind.CrewmatePermissionGrant || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        var grant = await proposalRepository.GetCrewmatePermissionGrantByProposalIdAsync(proposal.Id, cancellationToken);
        if (grant is null || grant.IsApplied)
        {
            return;
        }

        var membership = await membershipRepository.GetMembershipAsync(grant.TargetUserId, proposal.CrewId!.Value, cancellationToken);
        if (membership is null || membership.IsBanned)
        {
            grant.IsApplied = true;
            return;
        }

        if (grant.GrantType == CrewmatePermissionGrantType.AttachFiles)
        {
            membership.CanAttachFiles = true;
        }
        else
        {
            membership.CanCreateProposals = true;
        }

        grant.IsApplied = true;

        var targetUser = await userRepository.GetByIdWithProfileAsync(grant.TargetUserId, cancellationToken);
        if (targetUser is not null)
        {
            var body = grant.GrantType == CrewmatePermissionGrantType.AttachFiles
                ? $"{targetUser.Username} was granted file attachment permission."
                : $"{targetUser.Username} was granted proposal creation permission.";

            await notificationService.NotifyCrewAsync(
                proposal.CrewId!.Value,
                NotificationKind.NewProposal,
                "Permission grant approved",
                body,
                $"/app/crew/proposals/{proposal.Id}",
                relatedEntityId: grant.TargetUserId,
                cancellationToken: cancellationToken);
        }
    }
}
