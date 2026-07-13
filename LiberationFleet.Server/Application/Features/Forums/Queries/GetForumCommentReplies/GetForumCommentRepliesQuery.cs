using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Forums.Queries.GetForumCommentReplies;

public record GetForumCommentRepliesQuery(int PostId, int ParentCommentId) : IRequest<ForumCommentRepliesResponse>;

public class GetForumCommentRepliesQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IForumRepository forumRepository,
    ICryptoRepository cryptoRepository,
    IUserBlockRepository blockRepository) : IRequestHandler<GetForumCommentRepliesQuery, ForumCommentRepliesResponse>
{
    public async Task<ForumCommentRepliesResponse> Handle(GetForumCommentRepliesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ForumCommentRepliesResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var post = await forumRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
        {
            return new ForumCommentRepliesResponse { Success = false, Message = "Forum post not found." };
        }

        if (!post.CrewId.HasValue)
        {
            return new ForumCommentRepliesResponse { Success = false, Message = "Not a crew forum post." };
        }

        var crewId = post.CrewId.Value;
        if (!await membershipRepository.IsUserInCrewAsync(userId, crewId, cancellationToken))
        {
            return new ForumCommentRepliesResponse { Success = false, Message = "You are not in this crew." };
        }

        var parent = await forumRepository.GetCommentByIdAsync(request.ParentCommentId, cancellationToken);
        if (parent is null || parent.ForumPostId != post.Id)
        {
            return new ForumCommentRepliesResponse { Success = false, Message = "Parent comment not found." };
        }

        var threadRootId = CommentThread.GetThreadRootId(parent.Id, parent.ParentCommentId);
        var comments = await forumRepository.GetCommentsByPostIdAsync(post.Id, cancellationToken);
        var commentById = comments.ToDictionary(c => c.Id);
        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);
        var replies = comments
            .Where(c => c.ParentCommentId == threadRootId && !hiddenUserIds.Contains(c.AuthorUserId))
            .OrderBy(c => c.CreatedAt)
            .ToList();
        var replyIds = replies.Select(c => c.Id.ToString()).ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.ForumComment,
            replyIds,
            crewId,
            cancellationToken);
        var envelopeById = envelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var items = replies.Select(reply =>
        {
            envelopeById.TryGetValue(reply.Id.ToString(), out var envelope);
            var replyToUsername = ResolveReplyToUsername(reply, commentById, envelopeById);
            return ForumMapper.MapComment(reply, envelope, 0, replyToUsername);
        }).ToList();

        return new ForumCommentRepliesResponse
        {
            Success = true,
            Message = "Replies loaded.",
            Items = items
        };
    }

    private static string? ResolveReplyToUsername(
        ForumComment reply,
        IReadOnlyDictionary<int, ForumComment> commentById,
        IReadOnlyDictionary<string, EncryptedContentEnvelope> envelopeById)
    {
        if (!reply.ReplyToCommentId.HasValue
            || !commentById.TryGetValue(reply.ReplyToCommentId.Value, out var target))
        {
            return null;
        }

        if (envelopeById.ContainsKey(target.Id.ToString()))
        {
            return null;
        }

        return target.AuthorUser.Username;
    }
}
