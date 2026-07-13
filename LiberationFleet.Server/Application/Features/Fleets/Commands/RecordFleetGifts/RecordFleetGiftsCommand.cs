using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts;
using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGifts;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Commands.RecordFleetGifts;

public record RecordFleetGiftsCommand(IReadOnlyList<GiftRecordItem> Gifts) : IRequest<GiftOperationResponse>;

public class RecordFleetGiftsCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IFleetRepository fleetRepository,
    IGiftRepository giftRepository,
    ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
    IUserRepository userRepository,
    IMutualAidService mutualAidService,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<RecordFleetGiftsCommand, GiftOperationResponse>
{
    public async Task<GiftOperationResponse> Handle(RecordFleetGiftsCommand request, CancellationToken cancellationToken)
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

        var giverCrew = await crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
        if (giverCrew is null)
        {
            return new GiftOperationResponse { Success = false, Message = "Crew not found." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new GiftOperationResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        Gift? lastSaved = null;
        var notifiedRecipients = new HashSet<int>();

        foreach (var item in request.Gifts)
        {
            if (item.Amount <= 0)
            {
                return new GiftOperationResponse { Success = false, Message = "Gift amounts must be greater than zero." };
            }

            if (item.RecipientId == userId)
            {
                return new GiftOperationResponse { Success = false, Message = "You cannot give a gift to yourself." };
            }

            var recipientMembership = await membershipRepository.GetActiveMembershipAsync(item.RecipientId, cancellationToken);
            if (recipientMembership is null)
            {
                return new GiftOperationResponse { Success = false, Message = "Recipient is not in a crew." };
            }

            if (!await fleetRepository.IsCrewInFleetAsync(recipientMembership.CrewId, fleet.Id, cancellationToken))
            {
                return new GiftOperationResponse { Success = false, Message = "Recipient is not in your fleet." };
            }

            if (!giverCrew.AllowCrossCrewGiving && recipientMembership.CrewId != membership.CrewId)
            {
                return new GiftOperationResponse
                {
                    Success = false,
                    Message = "Your crew does not allow cross-crew giving."
                };
            }

            var giftCrewId = recipientMembership.CrewId;

            if (!await crewPaymentPlatformRepository.ExistsForCrewAsync(giftCrewId, item.PaymentPlatformId, cancellationToken)
                && !await crewPaymentPlatformRepository.ExistsForCrewAsync(membership.CrewId, item.PaymentPlatformId, cancellationToken))
            {
                return new GiftOperationResponse { Success = false, Message = "Invalid payment platform." };
            }

            if (item.MiddlemanId.HasValue)
            {
                if (item.MiddlemanId == userId || item.MiddlemanId == item.RecipientId)
                {
                    return new GiftOperationResponse { Success = false, Message = "Invalid intermediary selection." };
                }

                var intermediaryMembership = await membershipRepository.GetActiveMembershipAsync(
                    item.MiddlemanId.Value,
                    cancellationToken);
                if (intermediaryMembership is null
                    || !await fleetRepository.IsCrewInFleetAsync(intermediaryMembership.CrewId, fleet.Id, cancellationToken))
                {
                    return new GiftOperationResponse { Success = false, Message = "Intermediary is not in your fleet." };
                }

                if (!intermediaryMembership.IsIntermediary)
                {
                    return new GiftOperationResponse
                    {
                        Success = false,
                        Message = "Selected intermediary does not hold the Intermediary role."
                    };
                }
            }

            var isSurvivalThreshold = !item.IsCustom
                && string.Equals(item.EntryType, "survivalThreshold", StringComparison.OrdinalIgnoreCase);
            var countsTowardReception = !item.MiddlemanId.HasValue;

            var gift = new Gift
            {
                CrewId = giftCrewId,
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
                SeasonCycleId = item.SeasonCycleId,
                VerificationStatus = item.IsCustom
                    ? GiftVerificationStatus.Verified
                    : GiftVerificationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            await giftRepository.AddAsync(gift, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            if (item.IsCustom && countsTowardReception)
            {
                await mutualAidService.ApplyGiftReceptionAsync(gift, cancellationToken);
            }

            lastSaved = await giftRepository.GetByIdWithUsersAsync(gift.Id, cancellationToken);

            if (notifiedRecipients.Add(item.RecipientId))
            {
                var recipient = await userRepository.GetByIdWithProfileAsync(item.RecipientId, cancellationToken);
                if (recipient is not null && !recipient.IsUnclaimedPlaceholder)
                {
                    await notificationService.NotifyUserAsync(new CreateNotificationRequest
                    {
                        UserId = item.RecipientId,
                        CrewId = giftCrewId,
                        Kind = NotificationKind.NewFleetGifts,
                        Title = "New fleet gift(s)",
                        Body = "You received a new gift in your fleet.",
                        ActionUrl = "/app/fleet/gift-log",
                        RelatedEntityId = gift.Id
                    }, cancellationToken);
                }
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
