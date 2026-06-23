using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Forums.Queries.GetForumPostDetail;

public record GetForumPostDetailQuery(int PostId) : IRequest<ForumDetailResponse>;

public class GetForumPostDetailQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IForumRepository forumRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetForumPostDetailQuery, ForumDetailResponse>
{
    public async Task<ForumDetailResponse> Handle(GetForumPostDetailQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ForumDetailResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var post = await forumRepository.GetByIdWithAuthorAsync(request.PostId, cancellationToken);
        if (post is null)
        {
            return new ForumDetailResponse { Success = false, Message = "Forum post not found." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, post.CrewId, cancellationToken))
        {
            return new ForumDetailResponse { Success = false, Message = "You are not in this crew." };
        }

        var postEnvelope = await cryptoRepository.GetEnvelopeAsync(
            EncryptedContentType.ForumPost,
            post.Id.ToString(),
            cancellationToken);

        var comments = await forumRepository.GetCommentsByPostIdAsync(post.Id, cancellationToken);
        var topLevel = comments.Where(c => !c.ParentCommentId.HasValue).ToList();
        var commentIds = comments.Select(c => c.Id.ToString()).ToList();
        var commentEnvelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.ForumComment,
            commentIds,
            post.CrewId,
            cancellationToken);
        var commentEnvelopeById = commentEnvelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var commentDtos = topLevel.Select(comment =>
        {
            commentEnvelopeById.TryGetValue(comment.Id.ToString(), out var envelope);
            var replyCount = comments.Count(c => c.ParentCommentId == comment.Id);
            return ForumMapper.MapComment(comment, envelope, replyCount);
        }).ToList();

        return new ForumDetailResponse
        {
            Success = true,
            Message = "Forum post loaded.",
            Post = ForumMapper.MapDetail(post, postEnvelope, commentDtos, userId)
        };
    }
}
