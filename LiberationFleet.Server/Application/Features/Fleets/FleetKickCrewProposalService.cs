using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Fleets;

public sealed class FleetKickCrewResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int ProposalId { get; init; }

    public static FleetKickCrewResult Succeeded(int proposalId, string message) =>
        new() { Success = true, Message = message, ProposalId = proposalId };

    public static FleetKickCrewResult Failed(string message, int proposalId = 0) =>
        new() { Success = false, Message = message, ProposalId = proposalId };
}

public class FleetKickCrewProposalService(
    IProposalRepository proposalRepository,
    IFleetRepository fleetRepository,
    ICrewRepository crewRepository)
{
    public async Task<FleetKickCrewResult> CreateAsync(
        int fleetId,
        int authorUserId,
        int targetCrewId,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (!await fleetRepository.IsCrewInFleetAsync(targetCrewId, fleetId, cancellationToken))
        {
            return FleetKickCrewResult.Failed("That crew is not in this fleet.");
        }

        var existing = await proposalRepository.GetPendingFleetKickCrewAsync(fleetId, targetCrewId, cancellationToken);
        if (existing is not null)
        {
            return FleetKickCrewResult.Failed("A kick proposal for this crew is already pending.", existing.ProposalId);
        }

        var targetCrew = await crewRepository.GetByIdAsync(targetCrewId, cancellationToken);
        if (targetCrew is null)
        {
            return FleetKickCrewResult.Failed("Crew not found.");
        }

        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            FleetId = fleetId,
            AuthorUserId = authorUserId,
            Kind = ProposalKind.FleetKickCrew,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        ProposalVotingService.ApplyTimerRulesOnCreate(proposal, utcNow);
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);
        await proposalRepository.AddFleetKickCrewAsync(new ProposalFleetKickCrew
        {
            Proposal = proposal,
            TargetCrewId = targetCrewId,
            Reason = reason?.Trim(),
            Title = $"Remove {targetCrew.Name} from the fleet",
            Description = string.IsNullOrWhiteSpace(reason)
                ? $"Proposal to remove {targetCrew.Name} from the fleet."
                : $"Proposal to remove {targetCrew.Name} from the fleet. Reason: {reason.Trim()}"
        }, cancellationToken);

        return FleetKickCrewResult.Succeeded(proposal.Id, "Kick proposal submitted.");
    }

    public async Task TryApplyApprovedProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Kind != ProposalKind.FleetKickCrew || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        var kick = await proposalRepository.GetFleetKickCrewByProposalIdAsync(proposal.Id, cancellationToken);
        if (kick is null || kick.IsApplied || !proposal.FleetId.HasValue)
        {
            return;
        }

        var fleetCrew = await fleetRepository.GetFleetCrewAsync(proposal.FleetId.Value, kick.TargetCrewId, cancellationToken);
        if (fleetCrew is not null)
        {
            await fleetRepository.RemoveFleetCrewAsync(fleetCrew, cancellationToken);
        }

        var room = await fleetRepository.GetLinkedFleetChatRoomAsync(
            proposal.FleetId.Value,
            kick.TargetCrewId,
            cancellationToken);
        if (room is not null)
        {
            room.IsDeleted = true;
        }

        kick.IsApplied = true;
    }
}
