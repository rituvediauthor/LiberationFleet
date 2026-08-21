using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LiberationFleet.Server.Application.Features.Gifts.Queries.GetCrewGiftLog;

public record GetCrewGiftLogQuery(
    int Limit = 50,
    DateTime? BeforeCreatedAt = null,
    int? BeforeId = null) : IRequest<GiftLogResponse>;

public class GetCrewGiftLogQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    ICryptoRepository cryptoRepository,
    ILogger<GetCrewGiftLogQueryHandler> logger) : IRequestHandler<GetCrewGiftLogQuery, GiftLogResponse>
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

        GiftLogPage? page = null;
        Exception? pageLoadError = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                page = await giftRepository.GetLogPageByCrewIdAsync(
                    membership.CrewId,
                    limit,
                    request.BeforeCreatedAt,
                    request.BeforeId,
                    cancellationToken);
                pageLoadError = null;
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
            {
                pageLoadError = ex;
                logger.LogWarning(ex, "Gift log page load failed (attempt {Attempt}).", attempt + 1);
                if (attempt == 0)
                {
                    try
                    {
                        await giftRepository.EnsureGiftLogSchemaAsync(cancellationToken);
                    }
                    catch (Exception repairEx)
                    {
                        logger.LogWarning(repairEx, "Gift log schema repair during page load failed.");
                    }
                }
            }
        }

        if (page is null)
        {
            logger.LogError(pageLoadError, "Gift log page load failed after schema repair retry.");
            return new GiftLogResponse
            {
                Success = true,
                Message = "Gift log loaded with no entries (schema repair pending).",
                Items = Array.Empty<GiftLogEntryDto>(),
                HasMore = false
            };
        }

        var pageInitiatedIds = page.Items
            .Where(g => g.Type == GiftType.Initiated)
            .Select(g => g.Id)
            .ToList();

        IReadOnlyDictionary<int, Gift> completedByInitiated;
        try
        {
            completedByInitiated = await giftRepository.GetCompletedGiftsByInitiatedIdsAsync(
                membership.CrewId,
                pageInitiatedIds,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
        {
            logger.LogWarning(ex, "Gift log completed-initiated lookup failed; continuing without it.");
            completedByInitiated = new Dictionary<int, Gift>();
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
                logger.LogWarning(ex, "Batch initiated-parent load failed for gift log.");
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
                logger.LogWarning(ex, "Gift log completion-platform attach failed; continuing without options.");
            }
        }

        var pageGiftIds = page.Items.Select(g => g.Id).ToList();
        var giftIds = pageGiftIds.Select(id => id.ToString()).ToList();

        var envelopesTask = LoadEnvelopesAsync(membership.CrewId, giftIds, cancellationToken);
        var likesTask = SafeEnrichAsync(
            () => giftRepository.GetActiveLikeCountsForGiftsAsync(pageGiftIds, cancellationToken),
            new Dictionary<int, int>(),
            "Gift log like counts failed; continuing with zeros.");
        var likedTask = SafeEnrichAsync(
            () => giftRepository.GetActiveLikedGiftIdsByUserAsync(userId, pageGiftIds, cancellationToken),
            new HashSet<int>(),
            "Gift log liked-by-user lookup failed; continuing with none.");
        var commentsTask = SafeEnrichAsync(
            () => giftRepository.GetCommentCountsForGiftsAsync(pageGiftIds, cancellationToken),
            new Dictionary<int, int>(),
            "Gift log comment counts failed; continuing with zeros.");
        var seasonDatesTask = SafeEnrichAsync(
            () => giftRepository.GetSeasonStartDatesForGiftsAsync(pageGiftIds, cancellationToken),
            new Dictionary<int, DateTime?>(),
            "Gift log season start lookup failed; treating entries as unlocked.");

        await Task.WhenAll(envelopesTask, likesTask, likedTask, commentsTask, seasonDatesTask);

        var envelopeByGiftId = await envelopesTask;
        var likeCounts = await likesTask;
        var likedGiftIds = await likedTask;
        var commentCounts = await commentsTask;
        var seasonStartDates = await seasonDatesTask;

        var currentSeasonStartDate = membership.Crew?.CurrentSeasonStartDate;

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
                var isSeasonLocked = GiftSeasonAccess.IsSeasonLocked(
                    gift,
                    currentSeasonStartDate,
                    giftSeasonStartDate);
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
                    if (gift.Type is not GiftType.SeasonStarted
                        and not GiftType.CycleStarted
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
                logger.LogWarning(ex, "Skipping corrupt gift log entry {GiftId}.", gift.Id);
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
                    RelatedUserIds = new[] { gift.GiverUserId, gift.RecipientUserId }
                });
            }
        }

        return new GiftLogResponse
        {
            Success = true,
            Message = "Gift log loaded.",
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
            logger.LogWarning(ex, "Gift log envelope load failed; continuing without encrypted payloads.");
            return new Dictionary<string, EncryptedContentEnvelope>(StringComparer.Ordinal);
        }
    }
}
