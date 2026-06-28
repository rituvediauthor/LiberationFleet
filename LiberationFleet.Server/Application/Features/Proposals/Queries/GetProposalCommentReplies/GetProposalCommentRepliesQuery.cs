using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Proposals.Queries.GetProposalCommentReplies;

public record GetProposalCommentRepliesQuery(int ProposalId, int ParentCommentId) : IRequest<ProposalCommentRepliesResponse>;

public class ProposalCommentRepliesResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<ProposalCommentDto> Items { get; set; } = Array.Empty<ProposalCommentDto>();
}

public class GetProposalCommentRepliesQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IProposalRepository proposalRepository,
    ICryptoRepository cryptoRepository,
    ProposalAnonymousAliasService aliasService,
    IUserBlockRepository blockRepository) : IRequestHandler<GetProposalCommentRepliesQuery, ProposalCommentRepliesResponse>
{
    public async Task<ProposalCommentRepliesResponse> Handle(
        GetProposalCommentRepliesQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProposalCommentRepliesResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var proposal = await proposalRepository.GetByIdAsync(request.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return new ProposalCommentRepliesResponse { Success = false, Message = "Proposal not found." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, proposal.CrewId, cancellationToken))
        {
            return new ProposalCommentRepliesResponse { Success = false, Message = "You are not in this crew." };
        }

        var parent = await proposalRepository.GetCommentByIdAsync(request.ParentCommentId, cancellationToken);
        if (parent is null || parent.ProposalId != proposal.Id)
        {
            return new ProposalCommentRepliesResponse { Success = false, Message = "Comment not found." };
        }

        var allComments = await proposalRepository.GetCommentsByProposalIdAsync(proposal.Id, cancellationToken);
        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);
        var visibleComments = allComments
            .Where(c => !hiddenUserIds.Contains(c.AuthorUserId))
            .ToList();
        var replies = visibleComments
            .Where(c => c.ParentCommentId == request.ParentCommentId)
            .OrderByDescending(c => c.CreatedAt)
            .ToList();

        var replyIds = replies.Select(r => r.Id.ToString()).ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.ProposalComment,
            replyIds,
            proposal.CrewId,
            cancellationToken);
        var envelopeById = envelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var usesAnonymousComments = proposal.Kind == ProposalKind.General;
        var nicknameByUserId = usesAnonymousComments
            ? await aliasService.GetNicknameMapAsync(
                proposal.Id,
                replies.Select(r => r.AuthorUserId),
                cancellationToken)
            : new Dictionary<int, string>();

        var items = replies.Select(reply =>
        {
            envelopeById.TryGetValue(reply.Id.ToString(), out var envelope);
            var nestedReplyCount = visibleComments.Count(c => c.ParentCommentId == reply.Id);
            return ProposalMapper.MapComment(
                reply,
                envelope,
                nestedReplyCount,
                userId,
                usesAnonymousComments,
                nicknameByUserId);
        }).ToList();

        return new ProposalCommentRepliesResponse
        {
            Success = true,
            Message = "Replies loaded.",
            Items = items
        };
    }
}
