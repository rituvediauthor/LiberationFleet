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
    IFleetRepository fleetRepository,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork)
{
    public Task<CrewRoleProposalResult> CreateNominationAsync(
        int crewId,
        int authorUserId,
        int targetUserId,
        IReadOnlyList<CrewRole> roles,
        DateTime? representativeTermStartUtc,
        DateTime? representativeTermEndUtc,
        CancellationToken cancellationToken) =>
        CreateProposalAsync(
            crewId,
            authorUserId,
            targetUserId,
            CrewRoleProposalAction.Nominate,
            roles,
            representativeTermStartUtc,
            representativeTermEndUtc,
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
            representativeTermStartUtc: null,
            representativeTermEndUtc: null,
            cancellationToken);

    private async Task<CrewRoleProposalResult> CreateProposalAsync(
        int crewId,
        int authorUserId,
        int targetUserId,
        CrewRoleProposalAction action,
        IReadOnlyList<CrewRole> roles,
        DateTime? representativeTermStartUtc,
        DateTime? representativeTermEndUtc,
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

        CrewRoleMapper.ClearExpiredRepresentativeTerm(membership);

        if (action == CrewRoleProposalAction.Nominate)
        {
            roles = roles.Where(role => !CrewRoleMapper.HasRole(membership, role)).ToList();
            if (roles.Count == 0)
            {
                return CrewRoleProposalResult.Failed("This crewmate already holds the selected roles.");
            }

            if (roles.Contains(CrewRole.Representative))
            {
                var termError = ValidateRepresentativeTerm(representativeTermStartUtc, representativeTermEndUtc);
                if (termError is not null)
                {
                    return CrewRoleProposalResult.Failed(termError);
                }
            }
            else
            {
                representativeTermStartUtc = null;
                representativeTermEndUtc = null;
            }
        }
        else
        {
            roles = roles.Where(role => CrewRoleMapper.HasRole(membership, role)).ToList();
            if (roles.Count == 0)
            {
                return CrewRoleProposalResult.Failed("This crewmate does not hold the selected roles.");
            }

            representativeTermStartUtc = null;
            representativeTermEndUtc = null;
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
            ? BuildNominationDescription(targetUser.Username, roleList, roles, representativeTermStartUtc, representativeTermEndUtc)
            : $"Remove the following role(s) from {targetUser.Username}: {roleList}.";

        await proposalRepository.AddCrewRoleChangeAsync(new ProposalCrewRoleChange
        {
            Proposal = proposal,
            TargetUserId = targetUserId,
            Action = action,
            RolesJson = CrewRoleMapper.SerializeRoles(roles),
            Title = title,
            Description = description,
            RepresentativeTermStartUtc = representativeTermStartUtc,
            RepresentativeTermEndUtc = representativeTermEndUtc
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

        var membership = await membershipRepository.GetMembershipAsync(roleChange.TargetUserId, proposal.CrewId!.Value, cancellationToken);
        if (membership is null || membership.IsBanned)
        {
            roleChange.IsApplied = true;
            return;
        }

        var roles = CrewRoleMapper.DeserializeRoles(roleChange.RolesJson);
        CrewRoleMapper.ApplyRoles(
            membership,
            roles,
            assign: roleChange.Action == CrewRoleProposalAction.Nominate,
            representativeTermStartUtc: roleChange.RepresentativeTermStartUtc,
            representativeTermEndUtc: roleChange.RepresentativeTermEndUtc);

        roleChange.IsApplied = true;

        var targetUser = await userRepository.GetByIdWithProfileAsync(roleChange.TargetUserId, cancellationToken);
        if (targetUser is not null)
        {
            var roleLabels = string.Join(", ", roles.Select(CrewRoleMapper.GetDisplayName));
            var body = roleChange.Action == CrewRoleProposalAction.Nominate
                ? $"{targetUser.Username} was assigned: {roleLabels}."
                : $"{roleLabels} was removed from {targetUser.Username}.";

            await notificationService.NotifyCrewAsync(
                proposal.CrewId!.Value,
                NotificationKind.NewProposal,
                "Role change approved",
                body,
                $"/app/crew/proposals/{proposal.Id}",
                relatedEntityId: roleChange.TargetUserId,
                cancellationToken: cancellationToken);
        }
    }

    private static string? ValidateRepresentativeTerm(DateTime? startUtc, DateTime? endUtc)
    {
        if (!startUtc.HasValue || !endUtc.HasValue)
        {
            return "Representative nominations require a future start date and end date.";
        }

        var now = DateTime.UtcNow;
        if (startUtc.Value <= now)
        {
            return "Representative term start must be in the future.";
        }

        if (endUtc.Value <= startUtc.Value)
        {
            return "Representative term end must be after the start date.";
        }

        return null;
    }

    private static string BuildNominationDescription(
        string username,
        string roleList,
        IReadOnlyList<CrewRole> roles,
        DateTime? termStartUtc,
        DateTime? termEndUtc)
    {
        var description = $"Assign {username} the following crew role(s): {roleList}.";
        if (roles.Contains(CrewRole.Representative) && termStartUtc.HasValue && termEndUtc.HasValue)
        {
            description +=
                $" Representative term: {termStartUtc.Value:yyyy-MM-dd} through {termEndUtc.Value:yyyy-MM-dd} (UTC). " +
                "During the term they receive mutual aid ahead of cycles (except survival thresholds) so they can attend government functions for the crew.";
        }

        return description;
    }
}
