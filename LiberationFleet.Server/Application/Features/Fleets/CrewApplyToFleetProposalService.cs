using System.Text.Json;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Fleets;

public sealed class CrewApplyToFleetResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int ProposalId { get; init; }

    public static CrewApplyToFleetResult Succeeded(int proposalId, string message) =>
        new() { Success = true, Message = message, ProposalId = proposalId };

    public static CrewApplyToFleetResult Failed(string message, int proposalId = 0) =>
        new() { Success = false, Message = message, ProposalId = proposalId };
}

public class CrewApplyToFleetProposalService(
    IProposalRepository proposalRepository,
    IFleetRepository fleetRepository,
    ICrewRepository crewRepository,
    IChatRepository chatRepository,
    FleetJoinRequestProposalService fleetJoinRequestProposalService,
    ContentTenureService contentTenureService,
    IUnitOfWork unitOfWork)
{
    public async Task<CrewApplyToFleetResult> CreateAsync(
        int authorUserId,
        int applicantCrewId,
        Fleet fleet,
        string? joinCode,
        IReadOnlyList<int> acceptedRuleIds,
        CancellationToken cancellationToken,
        bool initiatedByFleetInvite = false)
    {
        if (await fleetRepository.GetFleetForCrewAsync(applicantCrewId, cancellationToken) is not null)
        {
            return CrewApplyToFleetResult.Failed("Your crew already belongs to a fleet.");
        }

        var existing = await proposalRepository.GetPendingCrewApplyToFleetAsync(
            applicantCrewId,
            fleet.Id,
            cancellationToken);
        if (existing is not null)
        {
            return CrewApplyToFleetResult.Failed(
                initiatedByFleetInvite
                    ? "That crew already has a pending invitation to this fleet."
                    : "Your crew already has a pending request to apply to this fleet.",
                existing.ProposalId);
        }

        var crew = await crewRepository.GetByIdAsync(applicantCrewId, cancellationToken);
        if (crew is null)
        {
            return CrewApplyToFleetResult.Failed("Crew not found.");
        }

        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            CrewId = applicantCrewId,
            AuthorUserId = authorUserId,
            Kind = ProposalKind.CrewApplyToFleet,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        ProposalVotingService.ApplyTimerRulesOnCreate(proposal, utcNow);
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);
        await proposalRepository.AddCrewApplyToFleetAsync(new ProposalCrewApplyToFleet
        {
            Proposal = proposal,
            FleetId = fleet.Id,
            TargetJoinCode = string.IsNullOrWhiteSpace(joinCode) ? null : joinCode.Trim().ToUpperInvariant(),
            AcceptedRuleIdsJson = JsonSerializer.Serialize(acceptedRuleIds.OrderBy(id => id)),
            Title = initiatedByFleetInvite
                ? $"Invitation to join fleet {fleet.Name}"
                : $"Apply to join fleet {fleet.Name}",
            Description = initiatedByFleetInvite
                ? $"{fleet.Name} invited {crew.Name} to join the fleet."
                : $"{crew.Name} wants to apply to join fleet {fleet.Name}.",
            InitiatedByFleetInvite = initiatedByFleetInvite
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

        return CrewApplyToFleetResult.Succeeded(
            proposal.Id,
            initiatedByFleetInvite
                ? "Fleet invitation submitted to the crew."
                : "Fleet join application submitted to your crew.");
    }

    public async Task TryApplyApprovedProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Kind != ProposalKind.CrewApplyToFleet || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        var apply = await proposalRepository.GetCrewApplyToFleetByProposalIdAsync(proposal.Id, cancellationToken);
        if (apply is null || apply.IsApplied || !proposal.CrewId.HasValue)
        {
            return;
        }

        if (await fleetRepository.IsCrewInFleetAsync(proposal.CrewId.Value, apply.FleetId, cancellationToken))
        {
            apply.IsApplied = true;
            apply.Description = "Crew was already in the fleet.";
            return;
        }

        if (await fleetRepository.GetFleetForCrewAsync(proposal.CrewId.Value, cancellationToken) is not null)
        {
            apply.IsApplied = true;
            apply.Description = "Crew joined another fleet first.";
            return;
        }

        var fleet = await fleetRepository.GetByIdAsync(apply.FleetId, cancellationToken);
        if (fleet is null)
        {
            apply.IsApplied = true;
            apply.Description = "Target fleet no longer exists.";
            return;
        }

        var crew = await crewRepository.GetByIdAsync(proposal.CrewId.Value, cancellationToken);
        if (crew is null)
        {
            return;
        }

        if (apply.InitiatedByFleetInvite)
        {
            await fleetRepository.AddFleetCrewAsync(new FleetCrew
            {
                FleetId = apply.FleetId,
                CrewId = proposal.CrewId.Value,
                JoinedAt = DateTime.UtcNow
            }, cancellationToken);
            await contentTenureService.OnCrewJoinedFleetAsync(
                proposal.CrewId.Value,
                apply.FleetId,
                cancellationToken);

            var existingRoom = await fleetRepository.GetLinkedFleetChatRoomAsync(
                apply.FleetId,
                proposal.CrewId.Value,
                cancellationToken);
            if (existingRoom is null)
            {
                await chatRepository.AddRoomAsync(new ChatRoom
                {
                    FleetId = apply.FleetId,
                    LinkedCrewId = proposal.CrewId.Value,
                    Name = crew.Name,
                    Purpose = $"Fleet chat for {crew.Name}",
                    RoomType = ChatRoomType.Text,
                    CreatedByUserId = proposal.AuthorUserId,
                    CreatedAt = DateTime.UtcNow,
                    LastActivityAt = DateTime.UtcNow
                }, cancellationToken);
            }

            apply.IsApplied = true;
            apply.Description = $"{crew.Name} accepted the fleet invitation and joined.";
            return;
        }

        await fleetJoinRequestProposalService.CreateFromCrewApplyAsync(
            proposal.AuthorUserId,
            apply.FleetId,
            proposal.CrewId.Value,
            cancellationToken);

        apply.IsApplied = true;
        apply.Description = "Crew approved applying to the fleet; a fleet join request was created.";
    }
}
