using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Queries.GetCrewGiftLog;

public record GetCrewGiftLogQuery(
    int Limit = 50,
    DateTime? BeforeCreatedAt = null,
    int? BeforeId = null) : IRequest<GiftLogResponse>;

public class GetCrewGiftLogQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetCrewGiftLogQuery, GiftLogResponse>
{
    public async Task<GiftLogResponse> Handle(GetCrewGiftLogQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new GiftLogResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new GiftLogResponse { Success = false, Message = "You are not in a crew." };
        }

        var limit = request.Limit <= 0 ? 50 : Math.Min(request.Limit, 100);
        var page = await giftRepository.GetLogPageByCrewIdAsync(
            membership.CrewId,
            limit,
            request.BeforeCreatedAt,
            request.BeforeId,
            cancellationToken);

        var completedByInitiated = await giftRepository.GetCompletedGiftsByInitiatedIdsAsync(membership.CrewId, cancellationToken);

        var initiatedParents = page.Items
            .Where(g => g.Type == GiftType.Initiated)
            .ToDictionary(g => g.Id, g => g);

        var missingParentIds = page.Items
            .Where(g => g.Type == GiftType.Completed && g.InitiatedGiftId.HasValue)
            .Select(g => g.InitiatedGiftId!.Value)
            .Where(id => !initiatedParents.ContainsKey(id))
            .Distinct()
            .ToList();

        foreach (var parentId in missingParentIds)
        {
            var parent = await giftRepository.GetByIdWithUsersAsync(parentId, cancellationToken);
            if (parent is not null)
            {
                initiatedParents[parentId] = parent;
            }
        }

        var pageGiftIds = page.Items.Select(g => g.Id).ToList();
        var giftIds = pageGiftIds.Select(id => id.ToString()).ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.GiftLogEntry,
            giftIds,
            crewId: membership.CrewId,
            cancellationToken: cancellationToken);
        var envelopeByGiftId = envelopes
            .GroupBy(e => e.ResourceId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // Engagement tables shipped with gift-log likes/comments; tolerate partial migrate.
        Dictionary<int, int> likeCounts;
        HashSet<int> likedGiftIds;
        Dictionary<int, int> commentCounts;
        try
        {
            likeCounts = await giftRepository.GetActiveLikeCountsForGiftsAsync(pageGiftIds, cancellationToken);
            likedGiftIds = await giftRepository.GetActiveLikedGiftIdsByUserAsync(userId, pageGiftIds, cancellationToken);
            commentCounts = await giftRepository.GetCommentCountsForGiftsAsync(pageGiftIds, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
        {
            likeCounts = new Dictionary<int, int>();
            likedGiftIds = [];
            commentCounts = new Dictionary<int, int>();
        }

        var seasonStartDates = await giftRepository.GetSeasonStartDatesForGiftsAsync(pageGiftIds, cancellationToken);
        var currentSeasonStartDate = membership.Crew?.CurrentSeasonStartDate;

        var items = page.Items.Select(gift =>
        {
            completedByInitiated.TryGetValue(gift.Id, out var completedChild);
            Gift? initiatedParent = null;
            if (gift.Type == GiftType.Completed && gift.InitiatedGiftId.HasValue)
            {
                initiatedParents.TryGetValue(gift.InitiatedGiftId.Value, out initiatedParent);
            }

            seasonStartDates.TryGetValue(gift.Id, out var giftSeasonStartDate);
            var isSeasonLocked = GiftSeasonAccess.IsSeasonLocked(
                gift,
                currentSeasonStartDate,
                giftSeasonStartDate ?? gift.SeasonCycle?.SeasonStartDate);
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
            if (envelopeByGiftId.TryGetValue(gift.Id.ToString(), out var envelope))
            {
                entry.HasEncryptedContent = true;
                entry.EncryptedPayload = CryptoMapper.MapPayload(envelope);
                // System / celebratory messages are not sensitive; keep plaintext so they
                // remain visible even when the encrypted gift envelope exists.
                if (gift.Type is not GiftType.SeasonStarted
                    and not GiftType.CycleStarted
                    and not GiftType.SurvivalThresholdsRefreshed)
                {
                    entry.GiverName = string.Empty;
                    entry.RecipientName = string.Empty;
                    entry.MiddlemanName = null;
                    entry.Platform = string.Empty;
                    entry.Message = string.Empty;
                }
            }

            return entry;
        }).ToList();

        return new GiftLogResponse
        {
            Success = true,
            Message = "Gift log loaded.",
            Items = items,
            HasMore = page.HasMore
        };
    }
}
