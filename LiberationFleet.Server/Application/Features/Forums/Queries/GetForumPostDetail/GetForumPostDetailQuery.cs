using LiberationFleet.Server.Application.Common;
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
    IUserRepository userRepository,
    IForumRepository forumRepository,
    ICryptoRepository cryptoRepository,
    IUserBlockRepository blockRepository) : IRequestHandler<GetForumPostDetailQuery, ForumDetailResponse>
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
            post.CrewId,
            cancellationToken);
        var commentEnvelopeById = commentEnvelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var commentDtos = topLevel.Select(comment =>
        {
            commentEnvelopeById.TryGetValue(comment.Id.ToString(), out var envelope);
            var replyCount = visibleComments.Count(c => c.ParentCommentId == comment.Id);
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
