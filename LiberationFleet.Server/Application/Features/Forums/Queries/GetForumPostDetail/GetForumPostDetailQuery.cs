using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Forums.Queries.GetForumPostDetail;

public record GetForumPostDetailQuery(int PostId) : IRequest<ForumDetailResponse>;

public class GetForumPostDetailQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    IForumRepository forumRepository,
    ICryptoRepository cryptoRepository,
    IUserBlockRepository blockRepository,
    CrewAvatarVisibilityService crewAvatarVisibility) : IRequestHandler<GetForumPostDetailQuery, ForumDetailResponse>
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

        if (!post.CrewId.HasValue)
        {
            return new ForumDetailResponse { Success = false, Message = "Not a crew forum post." };
        }

        var crewId = post.CrewId.Value;
        if (!await membershipRepository.IsUserInCrewAsync(userId, crewId, cancellationToken))
        {
            return new ForumDetailResponse { Success = false, Message = "You are not in this crew." };
        }

        var user = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        var preference = user?.AdultContentPreference ?? AdultContentPreference.Block;
        if (AdultContentAccess.IsBlocked(preference, post.IsAdultContent))
        {
            return new ForumDetailResponse { Success = false, Message = "Forum post not found." };
        }

        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);
        if (hiddenUserIds.Contains(post.AuthorUserId))
        {
            return new ForumDetailResponse { Success = false, Message = "Forum post not found." };
        }

        var postEnvelope = await cryptoRepository.GetEnvelopeAsync(
            EncryptedContentType.ForumPost,
            post.Id.ToString(),
            cancellationToken);

        var comments = await forumRepository.GetCommentsByPostIdAsync(post.Id, cancellationToken);
        var visibleComments = comments.Where(c => !hiddenUserIds.Contains(c.AuthorUserId)).ToList();
        var topLevel = visibleComments.Where(c => !c.ParentCommentId.HasValue).ToList();
        var commentIds = visibleComments.Select(c => c.Id.ToString()).ToList();
        var commentEnvelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.ForumComment,
            commentIds,
            crewId: crewId,
            cancellationToken: cancellationToken);
        var commentEnvelopeById = commentEnvelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var topLevelIds = topLevel.Select(c => c.Id).ToList();
        var commentLikeCounts = await forumRepository.GetActiveLikeCountsForCommentsAsync(topLevelIds, cancellationToken);
        var likedCommentIds = await forumRepository.GetActiveLikedCommentIdsByUserAsync(userId, topLevelIds, cancellationToken);
        var postLikeCounts = await forumRepository.GetActiveLikeCountsForPostsAsync([post.Id], cancellationToken);
        var likedPostIds = await forumRepository.GetActiveLikedPostIdsByUserAsync(userId, [post.Id], cancellationToken);
        postLikeCounts.TryGetValue(post.Id, out var postLikeCount);
        var avatarAllowed = await crewAvatarVisibility.GetUsersAllowedToShowCrewAvatarAsync(crewId, cancellationToken);

        var commentDtos = topLevel.Select(comment =>
        {
            commentEnvelopeById.TryGetValue(comment.Id.ToString(), out var envelope);
            var replyCount = visibleComments.Count(c => c.ParentCommentId == comment.Id);
            commentLikeCounts.TryGetValue(comment.Id, out var likeCount);
            return ForumMapper.MapComment(
                comment,
                envelope,
                replyCount,
                likeCount: likeCount,
                likedByCurrentUser: likedCommentIds.Contains(comment.Id),
                crewAvatarAllowedUserIds: avatarAllowed);
        }).ToList();

        return new ForumDetailResponse
        {
            Success = true,
            Message = "Forum post loaded.",
            Post = ForumMapper.MapDetail(
                post,
                postEnvelope,
                commentDtos,
                userId,
                postLikeCount,
                likedPostIds.Contains(post.Id),
                visibleComments.Count,
                avatarAllowed)
        };
    }
}
