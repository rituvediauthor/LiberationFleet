using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Gifts;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetGiftLog;

public record GetFleetGiftLogQuery(
    int Limit = 50,
    DateTime? BeforeCreatedAt = null,
    int? BeforeId = null) : IRequest<GiftLogResponse>;

public class GetFleetGiftLogQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IGiftRepository giftRepository,
    ICryptoRepository cryptoRepository,
    ILogger<GetFleetGiftLogQueryHandler> logger) : IRequestHandler<GetFleetGiftLogQuery, GiftLogResponse>
{
    public async Task<GiftLogResponse> Handle(GetFleetGiftLogQuery request, CancellationToken cancellationToken)
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

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new GiftLogResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        var crewIds = (await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken))
            .Select(fc => fc.CrewId)
            .ToList();
        if (crewIds.Count == 0)
        {
            return new GiftLogResponse
            {
                Success = true,
                Message = "Fleet gift log loaded.",
                Items = Array.Empty<GiftLogEntryDto>(),
                HasMore = false
            };
        }

        var limit = request.Limit <= 0 ? 50 : Math.Min(request.Limit, 100);
        var page = await giftRepository.GetLogPageByCrewIdsAsync(
            crewIds,
            limit,
            request.BeforeCreatedAt,
            request.BeforeId,
            cancellationToken);

        var pageInitiatedIds = page.Items
            .Where(g => g.Type == GiftType.Initiated)
            .Select(g => g.Id)
            .ToList();

        var completedByInitiated = new Dictionary<int, Gift>();
        foreach (var crewGroup in page.Items
            .Where(g => g.Type == GiftType.Initiated)
            .GroupBy(g => g.CrewId))
        {
            try
            {
                var completed = await giftRepository.GetCompletedGiftsByInitiatedIdsAsync(
                    crewGroup.Key,
                    crewGroup.Select(g => g.Id).ToList(),
                    cancellationToken);
                foreach (var pair in completed)
                {
                    completedByInitiated[pair.Key] = pair.Value;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
            {
                logger.LogWarning(ex, "Fleet gift log completed-initiated lookup failed for crew {CrewId}.", crewGroup.Key);
            }
        }

        var initiatedParents = page.Items
            .Where(g => g.Type == GiftType.Initiated)
            .ToDictionary(g => g.Id, g => g);

        var missingParentIds = page.Items
            .Where(g => g.Type == GiftType.Completed && g.InitiatedGiftId.HasValue)
            .Select(g => g.InitiatedGiftId!.Value)
            .Where(id => !initiatedParents.ContainsKey(id))
            .Distinct()
            .ToList();

        if (missingParentIds.Count > 0)
        {
            try
            {
                var parents = await giftRepository.GetGiftsByIdsWithUsersAsync(missingParentIds, cancellationToken);
                foreach (var parent in parents)
                {
                    initiatedParents[parent.Id] = parent;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
            {
                logger.LogWarning(ex, "Fleet gift log initiated-parent load failed.");
            }
        }

        var needsCompletionPlatforms = page.Items
            .Where(g =>
                g.Type == GiftType.Initiated
                && g.MiddlemanUserId == userId
                && !completedByInitiated.ContainsKey(g.Id)
                && g.VerificationStatus == GiftVerificationStatus.MiddlemanReceivedFunds
                && g.MiddlemanUser is not null
                && g.RecipientUser is not null)
            .ToList();
        if (needsCompletionPlatforms.Count > 0)
        {
            try
            {
                var usersNeedingPlatforms = needsCompletionPlatforms
                    .SelectMany(g => new[] { g.MiddlemanUser!, g.RecipientUser! })
                    .ToList();
                await giftRepository.AttachPaymentPlatformsToUsersAsync(usersNeedingPlatforms, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
            {
                logger.LogWarning(ex, "Fleet gift log completion-platform attach failed.");
            }
        }

        var pageGiftIds = page.Items.Select(g => g.Id).ToList();
        var homeCrewGiftIds = page.Items
            .Where(g => g.CrewId == membership.CrewId)
            .Select(g => g.Id.ToString())
            .ToList();

        // Only home-crew envelopes are decryptable by the viewer.
        var envelopeByGiftId = await LoadEnvelopesAsync(membership.CrewId, homeCrewGiftIds, cancellationToken);
        var likeCounts = await SafeEnrichAsync(
            () => giftRepository.GetActiveLikeCountsForGiftsAsync(pageGiftIds, cancellationToken),
            new Dictionary<int, int>(),
            "Fleet gift log like counts failed.");
        var likedGiftIds = await SafeEnrichAsync(
            () => giftRepository.GetActiveLikedGiftIdsByUserAsync(userId, pageGiftIds, cancellationToken),
            new HashSet<int>(),
            "Fleet gift log liked-by-user lookup failed.");
        var commentCounts = await SafeEnrichAsync(
            () => giftRepository.GetCommentCountsForGiftsAsync(pageGiftIds, cancellationToken),
            new Dictionary<int, int>(),
            "Fleet gift log comment counts failed.");

        var currentSeasonStartDate = membership.Crew?.CurrentSeasonStartDate;
        var seasonStartDates = await SafeEnrichAsync(
            () => giftRepository.GetSeasonStartDatesForGiftsAsync(pageGiftIds, cancellationToken),
            new Dictionary<int, DateTime?>(),
            "Fleet gift log season start lookup failed.");

        var items = new List<GiftLogEntryDto>(page.Items.Count);
        foreach (var gift in page.Items)
        {
            try
            {
                completedByInitiated.TryGetValue(gift.Id, out var completedChild);
                Gift? initiatedParent = null;
                if (gift.Type == GiftType.Completed && gift.InitiatedGiftId.HasValue)
                {
                    initiatedParents.TryGetValue(gift.InitiatedGiftId.Value, out initiatedParent);
                }

                seasonStartDates.TryGetValue(gift.Id, out var giftSeasonStartDate);
                // Season lock only applies to the viewer's home-crew gifts.
                var isSeasonLocked = gift.CrewId == membership.CrewId
                    && GiftSeasonAccess.IsSeasonLocked(gift, currentSeasonStartDate, giftSeasonStartDate);
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
                    isAccountant: gift.CrewId == membership.CrewId
                        && CrewRoleAuthorizationService.CanBypassSeasonGiftLock(membership));

                if (envelopeByGiftId.TryGetValue(gift.Id.ToString(), out var envelope))
                {
                    entry.HasEncryptedContent = true;
                    entry.EncryptedPayload = CryptoMapper.MapPayload(envelope);
                    if (gift.Type is not GiftType.SeasonStarted
                        and not GiftType.CycleStarted
                        and not GiftType.CycleCompleted
                        and not GiftType.SurvivalThresholdsRefreshed)
                    {
                        entry.GiverName = string.Empty;
                        entry.RecipientName = string.Empty;
                        entry.MiddlemanName = null;
                        entry.Platform = string.Empty;
                    }
                }

                items.Add(entry);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
            {
                logger.LogWarning(ex, "Skipping corrupt fleet gift log entry {GiftId}.", gift.Id);
                items.Add(new GiftLogEntryDto
                {
                    Id = gift.Id,
                    Type = gift.Type.ToString().ToLowerInvariant(),
                    GiverId = gift.GiverUserId,
                    RecipientId = gift.RecipientUserId,
                    Amount = gift.Amount,
                    Timestamp = gift.CreatedAt,
                    Message = "Unable to display this gift entry.",
                    VerificationStatus = gift.VerificationStatus.ToString(),
                    RelatedUserIds = new[] { gift.GiverUserId, gift.RecipientUserId },
                    CrewId = gift.CrewId
                });
            }
        }

        return new GiftLogResponse
        {
            Success = true,
            Message = "Fleet gift log loaded.",
            Items = items,
            HasMore = page.HasMore
        };
    }

    private async Task<T> SafeEnrichAsync<T>(Func<Task<T>> load, T fallback, string warning)
    {
        try
        {
            return await load();
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
        {
            logger.LogWarning(ex, warning);
            return fallback;
        }
    }

    private async Task<Dictionary<string, EncryptedContentEnvelope>> LoadEnvelopesAsync(
        int crewId,
        IReadOnlyList<string> giftIds,
        CancellationToken cancellationToken)
    {
        if (giftIds.Count == 0)
        {
            return new Dictionary<string, EncryptedContentEnvelope>(StringComparer.Ordinal);
        }

        try
        {
            var envelopes = await cryptoRepository.GetEnvelopesAsync(
                EncryptedContentType.GiftLogEntry,
                giftIds,
                crewId: crewId,
                cancellationToken: cancellationToken);
            return envelopes
                .GroupBy(e => e.ResourceId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
        {
            logger.LogWarning(ex, "Fleet gift log envelope load failed.");
            return new Dictionary<string, EncryptedContentEnvelope>(StringComparer.Ordinal);
        }
    }
}
