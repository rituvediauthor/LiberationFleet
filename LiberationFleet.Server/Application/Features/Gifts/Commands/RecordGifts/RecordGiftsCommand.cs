using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGifts;

public record GiftRecordItem(
    decimal Amount,
    int PaymentPlatformId,
    int RecipientId,
    int? MiddlemanId,
    bool IsCustom,
    string? EntryType,
    int? SeasonCycleId = null,
    int? ThresholdId = null);

public record RecordGiftsCommand(
    IReadOnlyList<GiftRecordItem> Gifts,
    int? ImpersonateAsUserId = null) : IRequest<GiftOperationResponse>;

public class RecordGiftsCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
    IUserRepository userRepository,
    IMutualAidRepository mutualAidRepository,
    IMutualAidService mutualAidService,
    ICustomGiftRecordingService customGiftRecordingService,
    ICrewContributionRefreshScheduler contributionRefreshScheduler,
    NotificationService notificationService,
    IUnitOfWork unitOfWork,
    ILogger<RecordGiftsCommandHandler> logger) : IRequestHandler<RecordGiftsCommand, GiftOperationResponse>
{
    public async Task<GiftOperationResponse> Handle(RecordGiftsCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new GiftOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (request.Gifts.Count == 0)
        {
            return new GiftOperationResponse { Success = false, Message = "No gifts to record." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null || !membership.IsInSeason)
        {
            return new GiftOperationResponse { Success = false, Message = "You must be in an active season to record gifts." };
        }

        var giverUserId = userId;
        int? impersonatedByUserId = null;
        if (request.ImpersonateAsUserId is int impersonateAs && impersonateAs != userId)
        {
            if (!CrewRoleAuthorizationService.CanImpersonateGiftGiver(membership))
            {
                return new GiftOperationResponse
                {
                    Success = false,
                    Message = "Only organizers and accountants can record gifts as another crewmate."
                };
            }

            var targetMembership = await membershipRepository.GetMembershipAsync(
                impersonateAs,
                membership.CrewId,
                cancellationToken);
            if (targetMembership is null
                || targetMembership.IsBanned
                || targetMembership.LeftAt is not null)
            {
                return new GiftOperationResponse
                {
                    Success = false,
                    Message = "Impersonation target is not an active crewmate."
                };
            }

            giverUserId = impersonateAs;
            impersonatedByUserId = userId;
        }

        Gift? lastSaved = null;
        var notifiedRecipients = new HashSet<int>();
        var recordedCount = 0;
        var pendingNotify = new List<(int RecipientId, int? MiddlemanId)>();
        int? lastTrackedGiftId = null;
        var validatedPlatformIds = new HashSet<int>();
        var validatedRecipientIds = new HashSet<int>();
        var validatedIntermediaryIds = new HashSet<int>();

        // Shared across SplitAmountAgainstEntryNeedAsync calls in this batch.
        Crew? crew = null;
        IReadOnlyList<PendingReceptionCredit>? pendingCredits = null;

        foreach (var item in request.Gifts)
        {
            if (item.Amount <= 0)
            {
                return new GiftOperationResponse { Success = false, Message = "Gift amounts must be greater than zero." };
            }

            if (!validatedPlatformIds.Contains(item.PaymentPlatformId))
            {
                if (!await crewPaymentPlatformRepository.ExistsForCrewAsync(
                        membership.CrewId,
                        item.PaymentPlatformId,
                        cancellationToken))
                {
                    return new GiftOperationResponse { Success = false, Message = "Invalid payment platform." };
                }

                validatedPlatformIds.Add(item.PaymentPlatformId);
            }

            if (item.RecipientId == giverUserId)
            {
                return new GiftOperationResponse { Success = false, Message = "You cannot give a gift to yourself." };
            }

            if (item.MiddlemanId == giverUserId || item.MiddlemanId == item.RecipientId)
            {
                return new GiftOperationResponse { Success = false, Message = "Invalid intermediary selection." };
            }

            if (!validatedRecipientIds.Contains(item.RecipientId))
            {
                if (!await membershipRepository.IsUserInCrewAsync(item.RecipientId, membership.CrewId, cancellationToken))
                {
                    return new GiftOperationResponse { Success = false, Message = "Recipient is not in your crew." };
                }

                validatedRecipientIds.Add(item.RecipientId);
            }

            if (item.MiddlemanId.HasValue && !validatedIntermediaryIds.Contains(item.MiddlemanId.Value))
            {
                if (!await membershipRepository.IsUserInCrewAsync(item.MiddlemanId.Value, membership.CrewId, cancellationToken))
                {
                    return new GiftOperationResponse { Success = false, Message = "Intermediary is not in your crew." };
                }

                var intermediaryMembership = await membershipRepository.GetMembershipAsync(
                    item.MiddlemanId.Value,
                    membership.CrewId,
                    cancellationToken);
                if (intermediaryMembership is null || !intermediaryMembership.IsIntermediary)
                {
                    return new GiftOperationResponse
                    {
                        Success = false,
                        Message = "Selected intermediary does not hold the Intermediary role."
                    };
                }

                validatedIntermediaryIds.Add(item.MiddlemanId.Value);
            }

            if (item.IsCustom)
            {
                // Custom path has its own SaveChanges / reception apply.
                if (recordedCount > 0 && lastTrackedGiftId is null)
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                var category = CustomGiftRecordingService.ParseCategory(item.EntryType);
                var (applied, other) = await customGiftRecordingService.RecordAsync(
                    membership.CrewId,
                    giverUserId,
                    item.RecipientId,
                    item.Amount,
                    item.PaymentPlatformId,
                    item.MiddlemanId,
                    category,
                    cancellationToken,
                    impersonatedByUserId);

                if (applied is null && other is null)
                {
                    return new GiftOperationResponse { Success = false, Message = "Could not record custom gift." };
                }

                recordedCount += (applied is not null ? 1 : 0) + (other is not null ? 1 : 0);
                var highlight = other ?? applied;
                lastTrackedGiftId = highlight?.Id;
                lastSaved = null;
            }
            else
            {
                var isSurvivalThreshold = string.Equals(
                    item.EntryType,
                    "survivalThreshold",
                    StringComparison.OrdinalIgnoreCase);
                var isRepresentativeGift = string.Equals(
                    item.EntryType,
                    "representative",
                    StringComparison.OrdinalIgnoreCase);
                var isCatchUp = string.Equals(item.EntryType, "catchUp", StringComparison.OrdinalIgnoreCase);
                var countsTowardReception = !item.MiddlemanId.HasValue;

                if (isSurvivalThreshold && item.ThresholdId.HasValue)
                {
                    var threshold = await mutualAidRepository.GetThresholdByIdAsync(
                        item.ThresholdId.Value,
                        cancellationToken);
                    if (threshold is null
                        || threshold.CrewId != membership.CrewId
                        || threshold.UserId != item.RecipientId
                        || threshold.Satisfied)
                    {
                        return new GiftOperationResponse
                        {
                            Success = false,
                            Message = "Invalid survival threshold entry."
                        };
                    }
                }

                var (applyAmount, overflowAmount) = await SplitAmountAgainstEntryNeedAsync(
                    membership.CrewId,
                    item,
                    isRepresentativeGift,
                    async () =>
                    {
                        crew ??= await mutualAidRepository.GetCrewAsync(membership.CrewId, cancellationToken);
                        pendingCredits ??= await giftRepository.GetPendingReceptionCreditsAsync(
                            membership.CrewId,
                            crew?.CurrentSeasonStartDate,
                            cancellationToken);
                        return pendingCredits!;
                    },
                    cancellationToken);

                if (applyAmount > 0m)
                {
                    CustomGiftCategory? category = null;
                    if (isSurvivalThreshold)
                    {
                        category = CustomGiftCategory.SurvivalThreshold;
                    }

                    var gift = new Gift
                    {
                        CrewId = membership.CrewId,
                        GiverUserId = giverUserId,
                        RecipientUserId = item.RecipientId,
                        MiddlemanUserId = item.MiddlemanId,
                        ImpersonatedByUserId = impersonatedByUserId,
                        Type = item.MiddlemanId.HasValue ? GiftType.Initiated : GiftType.Direct,
                        Amount = applyAmount,
                        CrewPaymentPlatformId = item.PaymentPlatformId,
                        IsSurvivalThreshold = isSurvivalThreshold,
                        IsRepresentativeGift = isRepresentativeGift,
                        IsCustomGift = false,
                        CustomGiftCategory = category,
                        CountsTowardReception = countsTowardReception,
                        CountsTowardContribution = true,
                        SeasonCycleId = item.SeasonCycleId,
                        MonthlySurvivalThresholdId = isSurvivalThreshold ? item.ThresholdId : null,
                        VerificationStatus = GiftVerificationStatus.Pending,
                        CreatedAt = DateTime.UtcNow
                    };

                    _ = isCatchUp;

                    await giftRepository.AddAsync(gift, cancellationToken);
                    recordedCount++;
                    lastTrackedGiftId = null; // assigned after batch save
                    lastSaved = gift;
                }

                if (overflowAmount > 0m)
                {
                    var overflowGift = new Gift
                    {
                        CrewId = membership.CrewId,
                        GiverUserId = giverUserId,
                        RecipientUserId = item.RecipientId,
                        MiddlemanUserId = item.MiddlemanId,
                        ImpersonatedByUserId = impersonatedByUserId,
                        Type = item.MiddlemanId.HasValue ? GiftType.Initiated : GiftType.Direct,
                        Amount = overflowAmount,
                        CrewPaymentPlatformId = item.PaymentPlatformId,
                        IsSurvivalThreshold = false,
                        IsRepresentativeGift = false,
                        IsCustomGift = true,
                        CustomGiftCategory = CustomGiftCategory.Other,
                        CountsTowardReception = false,
                        CountsTowardContribution = true,
                        VerificationStatus = GiftVerificationStatus.Verified,
                        ReceptionApplied = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    await giftRepository.AddAsync(overflowGift, cancellationToken);
                    recordedCount++;
                    lastTrackedGiftId = null;
                    lastSaved = overflowGift;
                }

                if (applyAmount <= 0m && overflowAmount <= 0m)
                {
                    return new GiftOperationResponse
                    {
                        Success = false,
                        Message = "Gift amount must be greater than zero."
                    };
                }
            }

            if (notifiedRecipients.Add(item.RecipientId))
            {
                pendingNotify.Add((item.RecipientId, item.MiddlemanId));
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (lastSaved is not null && lastSaved.Id > 0)
        {
            lastTrackedGiftId = lastSaved.Id;
        }

        if (lastTrackedGiftId is int highlightId)
        {
            lastSaved = await giftRepository.GetByIdWithUsersAsync(highlightId, cancellationToken) ?? lastSaved;
        }

        if (pendingNotify.Count > 0)
        {
            try
            {
                var giver = await userRepository.GetByIdWithProfileAsync(giverUserId, cancellationToken);
                var giverName = UserDisplay.Name(giver);
                var actionUrl = lastSaved is not null
                    ? $"/app/crew/gift-log?highlightId={lastSaved.Id}"
                    : "/app/crew/gift-log";

                var members = await membershipRepository.GetActiveMembersByCrewIdAsync(
                    membership.CrewId,
                    cancellationToken);
                var recipientIds = pendingNotify.Select(p => p.RecipientId).Distinct().ToList();
                var recipientsById = (await userRepository.GetByIdsAsync(recipientIds, cancellationToken))
                    .ToDictionary(u => u.Id);

                var requests = new List<CreateNotificationRequest>();
                foreach (var (recipientId, middlemanId) in pendingNotify)
                {
                    if (!recipientsById.TryGetValue(recipientId, out var recipient)
                        || recipient.IsUnclaimedPlaceholder)
                    {
                        continue;
                    }

                    var recipientName = UserDisplay.Name(recipient);
                    foreach (var member in members)
                    {
                        if (member.User is null || member.User.IsUnclaimedPlaceholder)
                        {
                            continue;
                        }

                        var kind = GiftEventNotificationKinds.ResolveForViewer(
                            member.UserId,
                            giverUserId,
                            recipientId,
                            middlemanId,
                            fleet: false);
                        if (kind is null)
                        {
                            continue;
                        }

                        requests.Add(GiftEventNotificationKinds.BuildRequest(
                            kind.Value,
                            member.UserId,
                            membership.CrewId,
                            giverName,
                            recipientName,
                            actionUrl,
                            lastSaved?.Id,
                            recipientId,
                            giverUserId,
                            fleet: false));
                    }
                }

                if (requests.Count > 0)
                {
                    await notificationService.NotifyUsersAsync(requests, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to notify crew after recording gifts in crew {CrewId}",
                    membership.CrewId);
            }
        }

        contributionRefreshScheduler.Schedule(membership.CrewId);

        return new GiftOperationResponse
        {
            Success = true,
            Message = recordedCount == 1 ? "Gift recorded." : $"{recordedCount} gifts recorded.",
            Entry = lastSaved is not null ? GiftMapper.MapGift(lastSaved) : null
        };
    }

    /// <summary>
    /// Caps the gift against the targeted entry's remaining need; excess becomes uncategorized.
    /// Representative / unlimited entries accept the full amount.
    /// </summary>
    private async Task<(decimal ApplyAmount, decimal OverflowAmount)> SplitAmountAgainstEntryNeedAsync(
        int crewId,
        GiftRecordItem item,
        bool isRepresentativeGift,
        Func<Task<IReadOnlyList<PendingReceptionCredit>>> loadPendingCredits,
        CancellationToken cancellationToken)
    {
        if (item.Amount <= 0m || isRepresentativeGift)
        {
            return (item.Amount, 0m);
        }

        decimal? remainingNeed = null;

        if (item.SeasonCycleId.HasValue)
        {
            var cycle = await mutualAidRepository.GetSeasonCycleByIdAsync(
                item.SeasonCycleId.Value,
                cancellationToken);
            if (cycle is null || cycle.CrewId != crewId || cycle.UserId != item.RecipientId)
            {
                return (item.Amount, 0m);
            }

            var room = Math.Max(0m, cycle.CycleCapAtStart - cycle.CycleReceived);
            var pending = await loadPendingCredits();
            var pendingForCycle = pending
                .Where(p => p.SeasonCycleId == cycle.Id)
                .Sum(p => p.Amount);
            remainingNeed = Math.Max(0m, room - pendingForCycle);
        }
        else if (item.ThresholdId.HasValue)
        {
            var threshold = await mutualAidRepository.GetThresholdByIdAsync(
                item.ThresholdId.Value,
                cancellationToken);
            if (threshold is null || threshold.CrewId != crewId || threshold.UserId != item.RecipientId)
            {
                return (item.Amount, 0m);
            }

            var room = Math.Max(0m, threshold.ThresholdAmount - threshold.ReceivedAmount);
            var pending = await loadPendingCredits();
            var pendingForThreshold = pending
                .Where(p => p.MonthlySurvivalThresholdId == threshold.Id)
                .Sum(p => p.Amount);
            remainingNeed = Math.Max(0m, room - pendingForThreshold);
        }

        if (remainingNeed is null)
        {
            return (item.Amount, 0m);
        }

        var apply = Math.Min(item.Amount, remainingNeed.Value);
        return (apply, item.Amount - apply);
    }
}
