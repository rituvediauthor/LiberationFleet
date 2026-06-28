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
    string? EntryType);

public record RecordGiftsCommand(IReadOnlyList<GiftRecordItem> Gifts) : IRequest<GiftOperationResponse>;

public class RecordGiftsCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
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
                return new GiftOperationResponse { Success = false, Message = "Invalid middleman selection." };
            }

            if (!await membershipRepository.IsUserInCrewAsync(item.RecipientId, membership.CrewId, cancellationToken))
            {
                return new GiftOperationResponse { Success = false, Message = "Recipient is not in your crew." };
            }

            if (item.MiddlemanId.HasValue
                && !await membershipRepository.IsUserInCrewAsync(item.MiddlemanId.Value, membership.CrewId, cancellationToken))
            {
                return new GiftOperationResponse { Success = false, Message = "Middleman is not in your crew." };
            }

            var isSurvivalThreshold = !item.IsCustom
                && string.Equals(item.EntryType, "survivalThreshold", StringComparison.OrdinalIgnoreCase);
            var countsTowardReception = !item.MiddlemanId.HasValue;

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
                IsCustomGift = item.IsCustom,
                CountsTowardReception = countsTowardReception,
                CountsTowardContribution = true,
                VerificationStatus = item.IsCustom
                    ? GiftVerificationStatus.Verified
                    : GiftVerificationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            await giftRepository.AddAsync(gift, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            lastSaved = await giftRepository.GetByIdWithUsersAsync(gift.Id, cancellationToken);

            if (notifiedRecipients.Add(item.RecipientId))
            {
                await notificationService.NotifyUserAsync(new CreateNotificationRequest
                {
                    UserId = item.RecipientId,
                    CrewId = membership.CrewId,
                    Kind = NotificationKind.NewGifts,
                    Title = "New gift(s)",
                    Body = "You received a new gift in your crew.",
                    ActionUrl = "/app/crew/gift-log",
                    RelatedEntityId = gift.Id
                }, cancellationToken);
            }
        }

        return new GiftOperationResponse
        {
            Success = true,
            Message = request.Gifts.Count == 1 ? "Gift recorded." : $"{request.Gifts.Count} gifts recorded.",
            Entry = lastSaved is not null ? GiftMapper.MapGift(lastSaved) : null
        };
    }
}
