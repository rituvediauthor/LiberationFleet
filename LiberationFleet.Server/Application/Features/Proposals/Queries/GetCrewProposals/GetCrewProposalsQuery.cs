using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Rules;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Proposals.Queries.GetCrewProposals;

public record GetCrewProposalsQuery(string Status) : IRequest<ProposalListResponse>;

public class GetCrewProposalsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IProposalRepository proposalRepository,
    ICryptoRepository cryptoRepository,
    CrewSettingsProposalService crewSettingsProposalService,
    CrewRulesProposalService crewRulesProposalService,
    CrewChatsProposalService crewChatsProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<GetCrewProposalsQuery, ProposalListResponse>
{
    public async Task<ProposalListResponse> Handle(GetCrewProposalsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProposalListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ProposalListResponse { Success = false, Message = "You are not in a crew." };
        }

        var status = ProposalMapper.ParseStatus(request.Status);
        var proposals = await proposalRepository.GetByCrewAndStatusAsync(membership.CrewId, status, cancellationToken);
        var utcNow = DateTime.UtcNow;

        foreach (var proposal in proposals)
        {
            var statusBefore = proposal.Status;
            ProposalVotingService.TryAutoApproveOnTimer(proposal, utcNow);
            await ProposalApprovalCoordinator.ProcessNewlyApprovedAsync(
                proposal,
                statusBefore,
                crewSettingsProposalService,
                crewRulesProposalService,
                crewChatsProposalService,
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var resourceIds = proposals
            .Where(p => p.Kind == ProposalKind.General)
            .Select(p => p.Id.ToString())
            .ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.Proposal,
            resourceIds,
            membership.CrewId,
            cancellationToken);
        var envelopeById = envelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var crewSettingChanges = await proposalRepository.GetCrewSettingChangesByProposalIdsAsync(
            proposals.Where(p => p.Kind == ProposalKind.CrewSettingChange).Select(p => p.Id),
            cancellationToken);

        var crewRuleChanges = await proposalRepository.GetCrewRuleChangesByProposalIdsAsync(
            proposals.Where(p => p.Kind == ProposalKind.CrewRuleChange).Select(p => p.Id),
            cancellationToken);

        var crewChatChanges = await proposalRepository.GetCrewChatChangesByProposalIdsAsync(
            proposals.Where(p => p.Kind == ProposalKind.CrewChatChange).Select(p => p.Id),
            cancellationToken);

        var items = new List<ProposalListItemDto>();
        foreach (var proposal in proposals)
        {
            EncryptedContentEnvelope? envelope = null;
            if (proposal.Kind == ProposalKind.General)
            {
                envelopeById.TryGetValue(proposal.Id.ToString(), out envelope);
            }

            crewSettingChanges.TryGetValue(proposal.Id, out var crewSettingChange);
            crewRuleChanges.TryGetValue(proposal.Id, out var crewRuleChange);
            crewChatChanges.TryGetValue(proposal.Id, out var crewChatChange);
            var vote = await proposalRepository.GetVoteAsync(proposal.Id, userId, cancellationToken);
            var currentUserVote = vote is null ? null : vote.IsApprove ? "approve" : "disapprove";
            items.Add(ProposalMapper.MapListItem(
                proposal,
                envelope,
                crewSettingChange,
                crewRuleChange,
                crewChatChange,
                currentUserVote));
        }

        return new ProposalListResponse
        {
            Success = true,
            Message = "Proposals loaded.",
            Items = items
        };
    }
}
