using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Domain.Entities;
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

        if (!await membershipRepository.IsUserInCrewAsync(userId, proposal.CrewId!.Value, cancellationToken))
        {
            return new ProposalCommentRepliesResponse { Success = false, Message = "You are not in this crew." };
        }

        var parent = await proposalRepository.GetCommentByIdAsync(request.ParentCommentId, cancellationToken);
        if (parent is null || parent.ProposalId != proposal.Id)
        {
            return new ProposalCommentRepliesResponse { Success = false, Message = "Comment not found." };
        }

        var threadRootId = CommentThread.GetThreadRootId(parent.Id, parent.ParentCommentId);
        var allComments = await proposalRepository.GetCommentsByProposalIdAsync(proposal.Id, cancellationToken);
        var commentById = allComments.ToDictionary(c => c.Id);
        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);
        var visibleComments = allComments
            .Where(c => !hiddenUserIds.Contains(c.AuthorUserId))
            .ToList();
        var replies = visibleComments
            .Where(c => c.ParentCommentId == threadRootId)
            .OrderBy(c => c.CreatedAt)
            .ToList();

        var replyIds = replies.Select(r => r.Id.ToString()).ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.ProposalComment,
            replyIds,
            proposal.CrewId!.Value,
            cancellationToken);
        var envelopeById = envelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var usesAnonymousComments = proposal.Kind == ProposalKind.General;
        var nicknameUserIds = replies
            .Select(r => r.AuthorUserId)
            .Concat(replies
                .Where(r => r.ReplyToCommentId.HasValue && commentById.ContainsKey(r.ReplyToCommentId.Value))
                .Select(r => commentById[r.ReplyToCommentId!.Value].AuthorUserId))
            .Distinct();
        var nicknameByUserId = usesAnonymousComments
            ? await aliasService.GetNicknameMapAsync(proposal.Id, nicknameUserIds, cancellationToken)
            : new Dictionary<int, string>();

        var items = replies.Select(reply =>
        {
            envelopeById.TryGetValue(reply.Id.ToString(), out var envelope);
            var replyToUsername = ResolveReplyToUsername(
                reply,
                commentById,
                envelopeById,
                usesAnonymousComments,
                nicknameByUserId);
            return ProposalMapper.MapComment(
                reply,
                envelope,
                0,
                userId,
                usesAnonymousComments,
                nicknameByUserId,
                replyToUsername);
        }).ToList();

        return new ProposalCommentRepliesResponse
        {
            Success = true,
            Message = "Replies loaded.",
            Items = items
        };
    }

    private static string? ResolveReplyToUsername(
        ProposalComment reply,
        IReadOnlyDictionary<int, ProposalComment> commentById,
        IReadOnlyDictionary<string, EncryptedContentEnvelope> envelopeById,
        bool usesAnonymousComments,
        IReadOnlyDictionary<int, string> nicknameByUserId)
    {
        if (!reply.ReplyToCommentId.HasValue
            || !commentById.TryGetValue(reply.ReplyToCommentId.Value, out var target))
        {
            return null;
        }

        if (usesAnonymousComments)
        {
            return nicknameByUserId.TryGetValue(target.AuthorUserId, out var nickname)
                ? nickname
                : ProposalMapper.AnonymousAuthor;
        }

        if (envelopeById.ContainsKey(target.Id.ToString()))
        {
            return null;
        }

        return target.AuthorUser.Username;
    }
}
