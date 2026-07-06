using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Crews;

public sealed class CrewRoleProposalResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int ProposalId { get; init; }

    public static CrewRoleProposalResult Succeeded(int proposalId, string message) =>
        new() { Success = true, ProposalId = proposalId, Message = message };

    public static CrewRoleProposalResult Failed(string message, int proposalId = 0) =>
        new() { Success = false, Message = message, ProposalId = proposalId };
}

public class CrewRoleProposalService(
    IProposalRepository proposalRepository,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    NotificationService notificationService)
{
    public Task<CrewRoleProposalResult> CreateNominationAsync(
        int crewId,
        int authorUserId,
        int targetUserId,
        IReadOnlyList<CrewRole> roles,
        CancellationToken cancellationToken) =>
        CreateProposalAsync(
            crewId,
            authorUserId,
            targetUserId,
            CrewRoleProposalAction.Nominate,
            roles,
            cancellationToken);

    public Task<CrewRoleProposalResult> CreateDemotionAsync(
        int crewId,
        int authorUserId,
        int targetUserId,
        IReadOnlyList<CrewRole> roles,
        CancellationToken cancellationToken) =>
        CreateProposalAsync(
            crewId,
            authorUserId,
            targetUserId,
            CrewRoleProposalAction.Demote,
            roles,
            cancellationToken);

    private async Task<CrewRoleProposalResult> CreateProposalAsync(
        int crewId,
        int authorUserId,
        int targetUserId,
        CrewRoleProposalAction action,
        IReadOnlyList<CrewRole> roles,
        CancellationToken cancellationToken)
    {
        if (roles.Count == 0)
        {
            return CrewRoleProposalResult.Failed("Select at least one role.");
        }

        var targetUser = await userRepository.GetByIdWithProfileAsync(targetUserId, cancellationToken);
        if (targetUser is null)
        {
            return CrewRoleProposalResult.Failed("Crewmate not found.");
        }

        var membership = await membershipRepository.GetMembershipAsync(targetUserId, crewId, cancellationToken);
        if (membership is null || membership.IsBanned)
        {
            return CrewRoleProposalResult.Failed("Crewmate not found.");
        }

        if (action == CrewRoleProposalAction.Nominate)
        {
            roles = roles.Where(role => !CrewRoleMapper.HasRole(membership, role)).ToList();
            if (roles.Count == 0)
            {
                return CrewRoleProposalResult.Failed("This crewmate already holds the selected roles.");
            }
        }
        else
        {
            roles = roles.Where(role => CrewRoleMapper.HasRole(membership, role)).ToList();
            if (roles.Count == 0)
            {
                return CrewRoleProposalResult.Failed("This crewmate does not hold the selected roles.");
            }
        }

        var pending = await proposalRepository.GetPendingCrewRoleChangeForTargetAsync(
            crewId,
            targetUserId,
            cancellationToken);
        if (pending is not null)
        {
            return CrewRoleProposalResult.Failed(
                "A role change proposal for this crewmate is already pending.",
                pending.ProposalId);
        }

        var roleLabels = roles.Select(CrewRoleMapper.GetDisplayName).ToList();
        var roleList = string.Join(", ", roleLabels);
        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            CrewId = crewId,
            AuthorUserId = authorUserId,
            Kind = ProposalKind.CrewRoleChange,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        ProposalVotingService.ApplyTimerRulesOnCreate(proposal, utcNow);
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);

        var title = action == CrewRoleProposalAction.Nominate
            ? $"Nominate {targetUser.Username} as {roleList}"
            : $"Remove {roleList} from {targetUser.Username}";
        var description = action == CrewRoleProposalAction.Nominate
            ? $"Assign {targetUser.Username} the following crew role(s): {roleList}."
            : $"Remove the following role(s) from {targetUser.Username}: {roleList}.";

        await proposalRepository.AddCrewRoleChangeAsync(new ProposalCrewRoleChange
        {
            Proposal = proposal,
            TargetUserId = targetUserId,
            Action = action,
            RolesJson = CrewRoleMapper.SerializeRoles(roles),
            Title = title,
            Description = description
        }, cancellationToken);

        await notificationService.NotifyCrewAsync(
            crewId,
            NotificationKind.NewProposal,
            "New proposal",
            $"A proposal was submitted to change roles for {targetUser.Username}.",
            $"/app/crew/proposals/{proposal.Id}",
            relatedEntityId: proposal.Id,
            excludeUserId: authorUserId,
            cancellationToken: cancellationToken);

        var message = action == CrewRoleProposalAction.Nominate
            ? "Role nomination proposal submitted."
            : "Role demotion proposal submitted.";

        return CrewRoleProposalResult.Succeeded(proposal.Id, message);
    }

    public async Task TryApplyApprovedProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Kind != ProposalKind.CrewRoleChange || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        var roleChange = await proposalRepository.GetCrewRoleChangeByProposalIdAsync(proposal.Id, cancellationToken);
        if (roleChange is null || roleChange.IsApplied)
        {
            return;
        }

        var membership = await membershipRepository.GetMembershipAsync(roleChange.TargetUserId, proposal.CrewId, cancellationToken);
        if (membership is null || membership.IsBanned)
        {
            roleChange.IsApplied = true;
            return;
        }

        var roles = CrewRoleMapper.DeserializeRoles(roleChange.RolesJson);
        CrewRoleMapper.ApplyRoles(
            membership,
            roles,
            assign: roleChange.Action == CrewRoleProposalAction.Nominate);

        roleChange.IsApplied = true;

        var targetUser = await userRepository.GetByIdWithProfileAsync(roleChange.TargetUserId, cancellationToken);
        if (targetUser is not null)
        {
            var roleLabels = string.Join(", ", roles.Select(CrewRoleMapper.GetDisplayName));
            var body = roleChange.Action == CrewRoleProposalAction.Nominate
                ? $"{targetUser.Username} was assigned: {roleLabels}."
                : $"{roleLabels} was removed from {targetUser.Username}.";

            await notificationService.NotifyCrewAsync(
                proposal.CrewId,
                NotificationKind.NewProposal,
                "Role change approved",
                body,
                $"/app/crew/proposals/{proposal.Id}",
                relatedEntityId: roleChange.TargetUserId,
                cancellationToken: cancellationToken);
        }
    }
}
