using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Fleets;

public class FleetJoinRequestProposalService(
    IProposalRepository proposalRepository,
    IFleetRepository fleetRepository,
    ICrewRepository crewRepository,
    IChatRepository chatRepository,
    ContentTenureService contentTenureService,
    IUnitOfWork unitOfWork)
{
    public async Task CreateFromCrewApplyAsync(
        int authorUserId,
        int fleetId,
        int applicantCrewId,
        CancellationToken cancellationToken)
    {
        var existing = await proposalRepository.GetPendingFleetJoinRequestAsync(fleetId, applicantCrewId, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var fleet = await fleetRepository.GetByIdAsync(fleetId, cancellationToken);
        var crew = await crewRepository.GetByIdAsync(applicantCrewId, cancellationToken);
        if (fleet is null || crew is null)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            FleetId = fleetId,
            AuthorUserId = authorUserId,
            Kind = ProposalKind.FleetJoinRequest,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        ProposalVotingService.ApplyTimerRulesOnCreate(proposal, utcNow);
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);
        await proposalRepository.AddFleetJoinRequestAsync(new ProposalFleetJoinRequest
        {
            Proposal = proposal,
            FleetId = fleetId,
            ApplicantCrewId = applicantCrewId,
            Title = $"Allow {crew.Name} to join the fleet",
            Description = $"{crew.Name} requested to join fleet {fleet.Name}."
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
    }

    public async Task TryApplyApprovedProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Kind != ProposalKind.FleetJoinRequest || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        var joinRequest = await proposalRepository.GetFleetJoinRequestByProposalIdAsync(proposal.Id, cancellationToken);
        if (joinRequest is null || joinRequest.IsApplied || !proposal.FleetId.HasValue)
        {
            return;
        }

        if (await fleetRepository.IsCrewInFleetAsync(joinRequest.ApplicantCrewId, joinRequest.FleetId, cancellationToken))
        {
            joinRequest.IsApplied = true;
            return;
        }

        if (await fleetRepository.GetFleetForCrewAsync(joinRequest.ApplicantCrewId, cancellationToken) is not null)
        {
            joinRequest.IsApplied = true;
            joinRequest.Description = "Applicant crew already belongs to another fleet.";
            return;
        }

        var crew = await crewRepository.GetByIdAsync(joinRequest.ApplicantCrewId, cancellationToken);
        if (crew is null)
        {
            return;
        }

        await fleetRepository.AddFleetCrewAsync(new FleetCrew
        {
            FleetId = joinRequest.FleetId,
            CrewId = joinRequest.ApplicantCrewId,
            JoinedAt = DateTime.UtcNow
        }, cancellationToken);
        await contentTenureService.OnCrewJoinedFleetAsync(
            joinRequest.ApplicantCrewId,
            joinRequest.FleetId,
            cancellationToken);

        var existingRoom = await fleetRepository.GetLinkedFleetChatRoomAsync(
            joinRequest.FleetId,
            joinRequest.ApplicantCrewId,
            cancellationToken);
        if (existingRoom is null)
        {
            await chatRepository.AddRoomAsync(new ChatRoom
            {
                FleetId = joinRequest.FleetId,
                LinkedCrewId = joinRequest.ApplicantCrewId,
                Name = crew.Name,
                Purpose = $"Fleet chat for {crew.Name}",
                RoomType = ChatRoomType.Text,
                CreatedByUserId = proposal.AuthorUserId,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            }, cancellationToken);
        }

        joinRequest.IsApplied = true;
        joinRequest.Description = $"{crew.Name} was approved and joined the fleet.";
    }
}
