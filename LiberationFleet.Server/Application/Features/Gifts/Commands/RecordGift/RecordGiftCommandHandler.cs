using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGift;

public class RecordGiftCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
    IMutualAidService mutualAidService,
    IUnitOfWork unitOfWork) : IRequestHandler<RecordGiftCommand, GiftOperationResponse>
{
    public async Task<GiftOperationResponse> Handle(RecordGiftCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new GiftOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new GiftOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        if (!await crewPaymentPlatformRepository.ExistsForCrewAsync(membership.CrewId, request.PaymentPlatformId, cancellationToken))
        {
            return new GiftOperationResponse { Success = false, Message = "Invalid payment platform." };
        }

        if (request.CompletingGiftId.HasValue)
        {
            return await CompleteMiddlemanGiftAsync(
                userId,
                membership.CrewId,
                request.CompletingGiftId.Value,
                request.PaymentPlatformId,
                cancellationToken);
        }

        return await RecordNewGiftAsync(
            userId,
            membership.CrewId,
            request.RecipientId!.Value,
            request.MiddlemanId,
            request.Amount,
            request.PaymentPlatformId,
            cancellationToken);
    }

    private async Task<GiftOperationResponse> CompleteMiddlemanGiftAsync(
        int userId,
        int crewId,
        int completingGiftId,
        int paymentPlatformId,
        CancellationToken cancellationToken)
    {
        var initiated = await giftRepository.GetByIdWithUsersAsync(completingGiftId, cancellationToken);
        if (initiated is null || initiated.CrewId != crewId || initiated.Type != GiftType.Initiated)
        {
            return new GiftOperationResponse { Success = false, Message = "Pending gift not found." };
        }

        if (initiated.MiddlemanUserId != userId)
        {
            return new GiftOperationResponse { Success = false, Message = "You are not the middleman for this gift." };
        }

        if (await giftRepository.HasCompletedInitiatedGiftAsync(initiated.Id, cancellationToken))
        {
            return new GiftOperationResponse { Success = false, Message = "This gift has already been completed." };
        }

        if (initiated.MiddlemanUser is null || initiated.RecipientUser is null)
        {
            return new GiftOperationResponse { Success = false, Message = "Pending gift not found." };
        }

        var commonPlatforms = CrewPaymentPlatformService.GetCommonPlatforms(initiated.MiddlemanUser, initiated.RecipientUser);
        if (!commonPlatforms.Any(p => p.Id == paymentPlatformId))
        {
            return new GiftOperationResponse { Success = false, Message = "Selected payment platform is not shared with the recipient." };
        }

        var gift = new Gift
        {
            CrewId = crewId,
            GiverUserId = initiated.GiverUserId,
            RecipientUserId = initiated.RecipientUserId,
            MiddlemanUserId = userId,
            Type = GiftType.Completed,
            Amount = initiated.Amount,
            CrewPaymentPlatformId = paymentPlatformId,
            InitiatedGiftId = initiated.Id,
            IsSurvivalThreshold = initiated.IsSurvivalThreshold,
            IsCustomGift = initiated.IsCustomGift,
            CountsTowardReception = true,
            CreatedAt = DateTime.UtcNow
        };

        await giftRepository.AddAsync(gift, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await mutualAidService.ApplyGiftReceptionAsync(gift, cancellationToken);

        var saved = await giftRepository.GetByIdWithUsersAsync(gift.Id, cancellationToken);
        return new GiftOperationResponse
        {
            Success = true,
            Message = "Gift completed.",
            Entry = saved is not null ? GiftMapper.MapGift(saved) : null
        };
    }

    private async Task<GiftOperationResponse> RecordNewGiftAsync(
        int userId,
        int crewId,
        int recipientId,
        int? middlemanId,
        decimal amount,
        int paymentPlatformId,
        CancellationToken cancellationToken)
    {
        if (recipientId == userId)
        {
            return new GiftOperationResponse { Success = false, Message = "You cannot give a gift to yourself." };
        }

        if (middlemanId == userId)
        {
            return new GiftOperationResponse { Success = false, Message = "You cannot be the middleman for your own gift." };
        }

        if (middlemanId == recipientId)
        {
            return new GiftOperationResponse { Success = false, Message = "The recipient cannot be the middleman." };
        }

        var recipientInCrew = await membershipRepository.IsUserInCrewAsync(recipientId, crewId, cancellationToken);
        if (!recipientInCrew)
        {
            return new GiftOperationResponse { Success = false, Message = "Recipient is not in your crew." };
        }

        if (middlemanId.HasValue)
        {
            var middlemanInCrew = await membershipRepository.IsUserInCrewAsync(middlemanId.Value, crewId, cancellationToken);
            if (!middlemanInCrew)
            {
                return new GiftOperationResponse { Success = false, Message = "Middleman is not in your crew." };
            }
        }

        var gift = new Gift
        {
            CrewId = crewId,
            GiverUserId = userId,
            RecipientUserId = recipientId,
            MiddlemanUserId = middlemanId,
            Type = middlemanId.HasValue ? GiftType.Initiated : GiftType.Direct,
            Amount = amount,
            CrewPaymentPlatformId = paymentPlatformId,
            CreatedAt = DateTime.UtcNow
        };

        await giftRepository.AddAsync(gift, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (!middlemanId.HasValue)
        {
            await mutualAidService.ApplyGiftReceptionAsync(gift, cancellationToken);
        }

        var saved = await giftRepository.GetByIdWithUsersAsync(gift.Id, cancellationToken);
        return new GiftOperationResponse
        {
            Success = true,
            Message = middlemanId.HasValue ? "Gift initiated." : "Gift recorded.",
            Entry = saved is not null ? GiftMapper.MapGift(saved) : null
        };
    }
}
