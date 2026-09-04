using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

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

public record RecordGiftsCommand(IReadOnlyList<GiftRecordItem> Gifts) : IRequest<GiftOperationResponse>;

public class RecordGiftsCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
    IUserRepository userRepository,
    IMutualAidRepository mutualAidRepository,
    IMutualAidService mutualAidService,
    ICustomGiftRecordingService customGiftRecordingService,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<RecordGiftsCommand, GiftOperationResponse>
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

        Gift? lastSaved = null;
        var notifiedRecipients = new HashSet<int>();
        var recordedCount = 0;

        foreach (var item in request.Gifts)
        {
            if (item.Amount <= 0)
            {
                return new GiftOperationResponse { Success = false, Message = "Gift amounts must be greater than zero." };
            }

            if (!await crewPaymentPlatformRepository.ExistsForCrewAsync(membership.CrewId, item.PaymentPlatformId, cancellationToken))
            {
                return new GiftOperationResponse { Success = false, Message = "Invalid payment platform." };
            }

            if (item.RecipientId == userId)
            {
                return new GiftOperationResponse { Success = false, Message = "You cannot give a gift to yourself." };
            }

            if (item.MiddlemanId == userId || item.MiddlemanId == item.RecipientId)
            {
                return new GiftOperationResponse { Success = false, Message = "Invalid intermediary selection." };
            }

            if (!await membershipRepository.IsUserInCrewAsync(item.RecipientId, membership.CrewId, cancellationToken))
            {
                return new GiftOperationResponse { Success = false, Message = "Recipient is not in your crew." };
            }

            if (item.MiddlemanId.HasValue
                && !await membershipRepository.IsUserInCrewAsync(item.MiddlemanId.Value, membership.CrewId, cancellationToken))
            {
                return new GiftOperationResponse { Success = false, Message = "Intermediary is not in your crew." };
            }

            if (item.MiddlemanId.HasValue)
            {
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
            }

            if (item.IsCustom)
            {
                var category = CustomGiftRecordingService.ParseCategory(item.EntryType);
                var (applied, other) = await customGiftRecordingService.RecordAsync(
                    membership.CrewId,
                    userId,
                    item.RecipientId,
                    item.Amount,
                    item.PaymentPlatformId,
                    item.MiddlemanId,
                    category,
                    cancellationToken);

                if (applied is null && other is null)
                {
                    return new GiftOperationResponse { Success = false, Message = "Could not record custom gift." };
                }

                recordedCount += (applied is not null ? 1 : 0) + (other is not null ? 1 : 0);
                var highlight = other ?? applied;
                lastSaved = highlight is not null
                    ? await giftRepository.GetByIdWithUsersAsync(highlight.Id, cancellationToken)
                    : null;
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

                var gift = new Gift
                {
                    CrewId = membership.CrewId,
                    GiverUserId = userId,
                    RecipientUserId = item.RecipientId,
                    MiddlemanUserId = item.MiddlemanId,
                    Type = item.MiddlemanId.HasValue ? GiftType.Initiated : GiftType.Direct,
                    Amount = item.Amount,
                    CrewPaymentPlatformId = item.PaymentPlatformId,
                    IsSurvivalThreshold = isSurvivalThreshold,
                    IsRepresentativeGift = isRepresentativeGift,
                    IsCustomGift = false,
                    CountsTowardReception = countsTowardReception,
                    CountsTowardContribution = true,
                    SeasonCycleId = item.SeasonCycleId,
                    MonthlySurvivalThresholdId = isSurvivalThreshold ? item.ThresholdId : null,
                    VerificationStatus = GiftVerificationStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                _ = isCatchUp;

                await giftRepository.AddAsync(gift, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                recordedCount++;
                lastSaved = await giftRepository.GetByIdWithUsersAsync(gift.Id, cancellationToken);
            }

            if (notifiedRecipients.Add(item.RecipientId))
            {
                var recipient = await userRepository.GetByIdWithProfileAsync(item.RecipientId, cancellationToken);
                if (recipient is not null && !recipient.IsUnclaimedPlaceholder)
                {
                    await notificationService.NotifyUserAsync(new CreateNotificationRequest
                    {
                        UserId = item.RecipientId,
                        CrewId = membership.CrewId,
                        Kind = NotificationKind.NewGifts,
                        Title = "New gift(s)",
                        Body = "You received a new gift in your crew.",
                        ActionUrl = lastSaved is not null
                            ? $"/app/crew/gift-log?highlightId={lastSaved.Id}"
                            : "/app/crew/gift-log",
                        RelatedEntityId = lastSaved?.Id
                    }, cancellationToken);
                }
            }
        }

        await mutualAidService.OnCrewContributionsChangedAsync(membership.CrewId, cancellationToken);

        return new GiftOperationResponse
        {
            Success = true,
            Message = recordedCount == 1 ? "Gift recorded." : $"{recordedCount} gifts recorded.",
            Entry = lastSaved is not null ? GiftMapper.MapGift(lastSaved) : null
        };
    }
}
