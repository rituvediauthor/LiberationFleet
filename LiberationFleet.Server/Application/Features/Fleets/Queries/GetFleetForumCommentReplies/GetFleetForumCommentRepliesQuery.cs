using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Domain.Entities;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetForumCommentReplies;

public record GetFleetForumCommentRepliesQuery(int PostId, int ParentCommentId) : IRequest<ForumCommentRepliesResponse>;

public class GetFleetForumCommentRepliesQueryHandler(
    ICurrentUserService currentUser,
    IFleetRepository fleetRepository,
    IForumRepository forumRepository,
    IUserBlockRepository blockRepository) : IRequestHandler<GetFleetForumCommentRepliesQuery, ForumCommentRepliesResponse>
{
    public async Task<ForumCommentRepliesResponse> Handle(
        GetFleetForumCommentRepliesQuery request,
        CancellationToken cancellationToken)
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

        if (!post.FleetId.HasValue)
        {
            return new ForumCommentRepliesResponse { Success = false, Message = "Not a fleet forum post." };
        }

        if (!await fleetRepository.IsUserInFleetAsync(userId, post.FleetId.Value, cancellationToken))
        {
            return new ForumCommentRepliesResponse { Success = false, Message = "You are not in this fleet." };
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

        var items = replies.Select(reply =>
        {
            var replyToUsername = ResolveReplyToUsername(reply, commentById);
            return ForumMapper.MapComment(reply, envelope: null, 0, replyToUsername);
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
        IReadOnlyDictionary<int, ForumComment> commentById)
    {
        if (!reply.ReplyToCommentId.HasValue
            || !commentById.TryGetValue(reply.ReplyToCommentId.Value, out var target))
        {
            return null;
        }

        return target.AuthorUser.Username;
    }
}
