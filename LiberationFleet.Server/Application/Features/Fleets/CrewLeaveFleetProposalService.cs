using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Fleets;

public sealed class CrewLeaveFleetResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int ProposalId { get; init; }

    public static CrewLeaveFleetResult Succeeded(int proposalId, string message) =>
        new() { Success = true, Message = message, ProposalId = proposalId };

    public static CrewLeaveFleetResult Failed(string message, int proposalId = 0) =>
        new() { Success = false, Message = message, ProposalId = proposalId };
}

public class CrewLeaveFleetProposalService(
    IProposalRepository proposalRepository,
    IFleetRepository fleetRepository,
    ICrewRepository crewRepository,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    ContentTenureService contentTenureService,
    IUnitOfWork unitOfWork)
{
    public async Task<CrewLeaveFleetResult> CreateAsync(
        int authorUserId,
        int crewId,
        int fleetId,
        CancellationToken cancellationToken)
    {
        var fleet = await fleetRepository.GetByIdAsync(fleetId, cancellationToken);
        if (fleet is null)
        {
            return CrewLeaveFleetResult.Failed("Fleet not found.");
        }

        if (!await fleetRepository.IsCrewInFleetAsync(crewId, fleetId, cancellationToken))
        {
            return CrewLeaveFleetResult.Failed("Your crew is not in this fleet.");
        }

        var authorMembership = await membershipRepository.GetActiveMembershipAsync(authorUserId, cancellationToken);
        if (authorMembership is null || authorMembership.CrewId != crewId)
        {
            return CrewLeaveFleetResult.Failed("You are not a member of this crew.");
        }

        var crew = await crewRepository.GetByIdAsync(crewId, cancellationToken);
        if (crew is null)
        {
            return CrewLeaveFleetResult.Failed("Crew not found.");
        }

        var (canPropose, proposeError) = await ProposalCreationAuthorization.EnsureCrewMemberCanCreateAsync(
            crew,
            authorMembership,
            giftRepository,
            contentTenureService,
            cancellationToken);
        if (!canPropose)
        {
            return CrewLeaveFleetResult.Failed(
                proposeError ?? "You are not allowed to create proposals yet.");
        }

        var existing = await proposalRepository.GetPendingCrewLeaveFleetAsync(crewId, fleetId, cancellationToken);
        if (existing is not null)
        {
            return CrewLeaveFleetResult.Failed(
                "A leave-fleet proposal for your crew is already pending.",
                existing.ProposalId);
        }

        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            CrewId = crewId,
            AuthorUserId = authorUserId,
            Kind = ProposalKind.CrewLeaveFleet,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        await ProposalVotingService.ApplyTimerRulesOnCreateAsync(
            proposal, utcNow, crewRepository, fleetRepository, cancellationToken);
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);
        await proposalRepository.AddCrewLeaveFleetAsync(new ProposalCrewLeaveFleet
        {
            Proposal = proposal,
            FleetId = fleetId,
            Title = $"Leave fleet {fleet.Name}",
            Description = $"{crew.Name} proposes to leave fleet {fleet.Name}. Access to other crews' library offerings and fleet content will end if this passes."
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

        return CrewLeaveFleetResult.Succeeded(proposal.Id, "Leave-fleet proposal submitted.");
    }

    public async Task TryApplyApprovedProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Kind != ProposalKind.CrewLeaveFleet || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        var leave = await proposalRepository.GetCrewLeaveFleetByProposalIdAsync(proposal.Id, cancellationToken);
        if (leave is null || leave.IsApplied || !proposal.CrewId.HasValue)
        {
            return;
        }

        var fleetCrew = await fleetRepository.GetFleetCrewAsync(leave.FleetId, proposal.CrewId.Value, cancellationToken);
        if (fleetCrew is not null)
        {
            await contentTenureService.OnCrewLeftFleetAsync(
                proposal.CrewId.Value,
                leave.FleetId,
                cancellationToken);
            await fleetRepository.RemoveFleetCrewAsync(fleetCrew, cancellationToken);
        }

        var room = await fleetRepository.GetLinkedFleetChatRoomAsync(
            leave.FleetId,
            proposal.CrewId.Value,
            cancellationToken);
        if (room is not null)
        {
            room.IsDeleted = true;
            room.LinkedCrewId = null;
        }

        leave.IsApplied = true;
    }
}
