using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Queries.GetGiftDetail;

public record GetGiftDetailQuery(int GiftId) : IRequest<GiftDetailResponse>;

public class GetGiftDetailQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    ICryptoRepository cryptoRepository,
    IUserBlockRepository blockRepository,
    CrewAvatarVisibilityService crewAvatarVisibility) : IRequestHandler<GetGiftDetailQuery, GiftDetailResponse>
{
    public async Task<GiftDetailResponse> Handle(GetGiftDetailQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new GiftDetailResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new GiftDetailResponse { Success = false, Message = "You are not in a crew." };
        }

        var gift = await giftRepository.GetByIdWithUsersAsync(request.GiftId, cancellationToken);
        if (gift is null || gift.CrewId != membership.CrewId)
        {
            return new GiftDetailResponse { Success = false, Message = "Gift not found." };
        }

        var completedByInitiated = await giftRepository.GetCompletedGiftsByInitiatedIdsAsync(
            membership.CrewId,
            gift.Type == GiftType.Initiated ? [gift.Id] : null,
            cancellationToken);
        completedByInitiated.TryGetValue(gift.Id, out var completedChild);

        Gift? initiatedParent = null;
        if (gift.Type == GiftType.Completed && gift.InitiatedGiftId.HasValue)
        {
            initiatedParent = await giftRepository.GetByIdWithUsersAsync(gift.InitiatedGiftId.Value, cancellationToken);
        }

        var seasonStartDates = await giftRepository.GetSeasonStartDatesForGiftsAsync([gift.Id], cancellationToken);
        seasonStartDates.TryGetValue(gift.Id, out var giftSeasonStartDate);
        var currentSeasonStartDate = membership.Crew?.CurrentSeasonStartDate;
        var isSeasonLocked = GiftSeasonAccess.IsSeasonLocked(
            gift,
            currentSeasonStartDate,
            giftSeasonStartDate);

        Dictionary<int, int> likeCounts;
        HashSet<int> likedGiftIds;
        Dictionary<int, int> commentCounts;
        try
        {
            likeCounts = await giftRepository.GetActiveLikeCountsForGiftsAsync([gift.Id], cancellationToken);
            likedGiftIds = await giftRepository.GetActiveLikedGiftIdsByUserAsync(userId, [gift.Id], cancellationToken);
            commentCounts = await giftRepository.GetCommentCountsForGiftsAsync([gift.Id], cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
        {
            likeCounts = new Dictionary<int, int>();
            likedGiftIds = [];
            commentCounts = new Dictionary<int, int>();
        }
        likeCounts.TryGetValue(gift.Id, out var likeCount);
        commentCounts.TryGetValue(gift.Id, out var commentCount);

        var entry = GiftMapper.MapGift(
            gift,
            userId,
            completedChild,
            initiatedParent,
            likeCount: likeCount,
            likedByCurrentUser: likedGiftIds.Contains(gift.Id),
            commentCount: commentCount,
            isSeasonLocked: isSeasonLocked,
            isAccountant: membership.IsAccountant);

        var giftEnvelope = await cryptoRepository.GetEnvelopeAsync(
            EncryptedContentType.GiftLogEntry,
            gift.Id.ToString(),
            cancellationToken);
        if (giftEnvelope is not null)
        {
            entry.HasEncryptedContent = true;
            entry.EncryptedPayload = CryptoMapper.MapPayload(giftEnvelope);
            if (gift.Type is not GiftType.SeasonStarted
                and not GiftType.CycleStarted
                and not GiftType.SurvivalThresholdsRefreshed)
            {
                entry.GiverName = string.Empty;
                entry.RecipientName = string.Empty;
                entry.MiddlemanName = null;
                entry.Platform = string.Empty;
                // Keep FormatMessage as plaintext fallback; clients prefer payload.message.
            }
        }

        IReadOnlyList<GiftComment> comments;
        try
        {
            comments = await giftRepository.GetCommentsByGiftIdAsync(gift.Id, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
        {
            comments = Array.Empty<GiftComment>();
        }
        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);
        var visibleComments = comments.Where(c => !hiddenUserIds.Contains(c.AuthorUserId)).ToList();
        var topLevel = visibleComments.Where(c => !c.ParentCommentId.HasValue).ToList();
        var commentIds = visibleComments.Select(c => c.Id.ToString()).ToList();
        var commentEnvelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.GiftComment,
            commentIds,
            crewId: membership.CrewId,
            cancellationToken: cancellationToken);
        var commentEnvelopeById = commentEnvelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var topLevelIds = topLevel.Select(c => c.Id).ToList();
        var commentLikeCounts = await giftRepository.GetActiveLikeCountsForGiftCommentsAsync(topLevelIds, cancellationToken);
        var likedCommentIds = await giftRepository.GetActiveLikedGiftCommentIdsByUserAsync(userId, topLevelIds, cancellationToken);
        var avatarAllowed = await crewAvatarVisibility.GetUsersAllowedToShowCrewAvatarAsync(membership.CrewId, cancellationToken);

        var commentDtos = topLevel.Select(comment =>
        {
            commentEnvelopeById.TryGetValue(comment.Id.ToString(), out var envelope);
            var replyCount = visibleComments.Count(c => c.ParentCommentId == comment.Id);
            commentLikeCounts.TryGetValue(comment.Id, out var commentLikeCount);
            return GiftEngagementMapper.MapComment(
                comment,
                envelope,
                replyCount,
                likeCount: commentLikeCount,
                likedByCurrentUser: likedCommentIds.Contains(comment.Id),
                crewAvatarAllowedUserIds: avatarAllowed);
        }).ToList();

        return new GiftDetailResponse
        {
            Success = true,
            Message = "Gift loaded.",
            Entry = GiftEngagementMapper.MapDetail(entry, commentDtos)
        };
    }
}
