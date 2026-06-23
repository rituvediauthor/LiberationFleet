using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Forums.Queries.GetForumCommentReplies;

public record GetForumCommentRepliesQuery(int PostId, int ParentCommentId) : IRequest<ForumCommentRepliesResponse>;

public class GetForumCommentRepliesQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IForumRepository forumRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetForumCommentRepliesQuery, ForumCommentRepliesResponse>
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

        if (!await membershipRepository.IsUserInCrewAsync(userId, post.CrewId, cancellationToken))
        {
            return new ForumCommentRepliesResponse { Success = false, Message = "You are not in this crew." };
        }

        var parent = await forumRepository.GetCommentByIdAsync(request.ParentCommentId, cancellationToken);
        if (parent is null || parent.ForumPostId != post.Id)
        {
            return new ForumCommentRepliesResponse { Success = false, Message = "Parent comment not found." };
        }

        var comments = await forumRepository.GetCommentsByPostIdAsync(post.Id, cancellationToken);
        var replies = comments.Where(c => c.ParentCommentId == parent.Id).ToList();
        var replyIds = replies.Select(c => c.Id.ToString()).ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.ForumComment,
            replyIds,
            post.CrewId,
            cancellationToken);
        var envelopeById = envelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var items = replies.Select(reply =>
        {
            envelopeById.TryGetValue(reply.Id.ToString(), out var envelope);
            return ForumMapper.MapComment(reply, envelope, 0);
        }).ToList();

        return new ForumCommentRepliesResponse
        {
            Success = true,
            Message = "Replies loaded.",
            Items = items
        };
    }
}
