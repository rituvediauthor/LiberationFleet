using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Proposals.Queries.GetProposalDetail;

public record GetProposalDetailQuery(int ProposalId) : IRequest<ProposalDetailResponse>;

public class GetProposalDetailQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IProposalRepository proposalRepository,
    ICryptoRepository cryptoRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<GetProposalDetailQuery, ProposalDetailResponse>
{
    public async Task<ProposalDetailResponse> Handle(GetProposalDetailQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProposalDetailResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var proposal = await proposalRepository.GetByIdWithAuthorAsync(request.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return new ProposalDetailResponse { Success = false, Message = "Proposal not found." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, proposal.CrewId, cancellationToken))
        {
            return new ProposalDetailResponse { Success = false, Message = "You are not in this crew." };
        }

        var utcNow = DateTime.UtcNow;
        ProposalVotingService.TryAutoApproveOnTimer(proposal, utcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var proposalEnvelope = await cryptoRepository.GetEnvelopeAsync(
            EncryptedContentType.Proposal,
            proposal.Id.ToString(),
            cancellationToken);

        var comments = await proposalRepository.GetCommentsByProposalIdAsync(proposal.Id, cancellationToken);
        var topLevel = comments.Where(c => !c.ParentCommentId.HasValue).ToList();
        var commentIds = comments.Select(c => c.Id.ToString()).ToList();
        var commentEnvelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.ProposalComment,
            commentIds,
            proposal.CrewId,
            cancellationToken);
        var commentEnvelopeById = commentEnvelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var commentDtos = topLevel.Select(comment =>
        {
            commentEnvelopeById.TryGetValue(comment.Id.ToString(), out var envelope);
            var replyCount = comments.Count(c => c.ParentCommentId == comment.Id);
            return ProposalMapper.MapComment(comment, envelope, replyCount);
        }).ToList();

        var vote = await proposalRepository.GetVoteAsync(proposal.Id, userId, cancellationToken);
        var currentUserVote = vote is null ? null : vote.IsApprove ? "approve" : "disapprove";

        return new ProposalDetailResponse
        {
            Success = true,
            Message = "Proposal loaded.",
            Proposal = ProposalMapper.MapDetail(proposal, proposalEnvelope, commentDtos, userId, currentUserVote)
        };
    }
}
