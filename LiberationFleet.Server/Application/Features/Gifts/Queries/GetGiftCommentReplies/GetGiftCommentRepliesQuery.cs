using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Queries.GetGiftCommentReplies;

public record GetGiftCommentRepliesQuery(int GiftId, int ParentCommentId) : IRequest<GiftCommentRepliesResponse>;

public class GetGiftCommentRepliesQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    ICryptoRepository cryptoRepository,
    IUserBlockRepository blockRepository,
    CrewAvatarVisibilityService crewAvatarVisibility) : IRequestHandler<GetGiftCommentRepliesQuery, GiftCommentRepliesResponse>
{
    public async Task<GiftCommentRepliesResponse> Handle(
        GetGiftCommentRepliesQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new GiftCommentRepliesResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new GiftCommentRepliesResponse { Success = false, Message = "You are not in a crew." };
        }

        var gift = await giftRepository.GetByIdWithUsersAsync(request.GiftId, cancellationToken);
        if (gift is null || gift.CrewId != membership.CrewId)
        {
            return new GiftCommentRepliesResponse { Success = false, Message = "Gift not found." };
        }

        var parent = await giftRepository.GetCommentByIdAsync(request.ParentCommentId, cancellationToken);
        if (parent is null || parent.GiftId != gift.Id)
        {
            return new GiftCommentRepliesResponse { Success = false, Message = "Parent comment not found." };
        }

        var threadRootId = CommentThread.GetThreadRootId(parent.Id, parent.ParentCommentId);
        var comments = await giftRepository.GetCommentsByGiftIdAsync(gift.Id, cancellationToken);
        var commentById = comments.ToDictionary(c => c.Id);
        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);
        var replies = comments
            .Where(c => c.ParentCommentId == threadRootId && !hiddenUserIds.Contains(c.AuthorUserId))
            .OrderBy(c => c.CreatedAt)
            .ToList();
        var replyIds = replies.Select(c => c.Id.ToString()).ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.GiftComment,
            replyIds,
            crewId: membership.CrewId,
            cancellationToken: cancellationToken);
        var envelopeById = envelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var replyCommentIds = replies.Select(r => r.Id).ToList();
        var commentLikeCounts = await giftRepository.GetActiveLikeCountsForGiftCommentsAsync(replyCommentIds, cancellationToken);
        var likedCommentIds = await giftRepository.GetActiveLikedGiftCommentIdsByUserAsync(userId, replyCommentIds, cancellationToken);
        var avatarAllowed = await crewAvatarVisibility.GetUsersAllowedToShowCrewAvatarAsync(membership.CrewId, cancellationToken);

        var items = replies.Select(reply =>
        {
            envelopeById.TryGetValue(reply.Id.ToString(), out var envelope);
            var replyToUsername = ResolveReplyToUsername(reply, commentById, envelopeById);
            commentLikeCounts.TryGetValue(reply.Id, out var likeCount);
            return GiftEngagementMapper.MapComment(
                reply,
                envelope,
                0,
                replyToUsername,
                likeCount,
                likedCommentIds.Contains(reply.Id),
                avatarAllowed);
        }).ToList();

        return new GiftCommentRepliesResponse
        {
            Success = true,
            Message = "Replies loaded.",
            Items = items
        };
    }

    private static string? ResolveReplyToUsername(
        GiftComment reply,
        IReadOnlyDictionary<int, GiftComment> commentById,
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
