using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Commands.VerifyGift;

public record VerifyGiftRequest(string Action, int? PaymentPlatformId = null);

public record VerifyGiftCommand(int GiftId, GiftVerificationAction Action, int? PaymentPlatformId = null)
    : IRequest<GiftOperationResponse>;

public class VerifyGiftCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
    IMutualAidService mutualAidService,
    IUnitOfWork unitOfWork) : IRequestHandler<VerifyGiftCommand, GiftOperationResponse>
{
    public async Task<GiftOperationResponse> Handle(VerifyGiftCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new GiftOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null || !membership.IsInSeason)
        {
            return new GiftOperationResponse { Success = false, Message = "You must be in an active season to verify gifts." };
        }

        var gift = await giftRepository.GetByIdWithUsersAsync(request.GiftId, cancellationToken);
        if (gift is null || gift.CrewId != membership.CrewId)
        {
            return new GiftOperationResponse { Success = false, Message = "Gift not found." };
        }

        if (gift.IsCustomGift)
        {
            return new GiftOperationResponse { Success = false, Message = "Custom gifts do not require verification." };
        }

        Gift? completedChild = null;
        Gift? initiatedParent = null;

        if (gift.Type == GiftType.Initiated)
        {
            completedChild = await giftRepository.GetCompletedGiftForInitiatedAsync(gift.Id, cancellationToken);
        }
        else if (gift.Type == GiftType.Completed && gift.InitiatedGiftId.HasValue)
        {
            initiatedParent = await giftRepository.GetByIdWithUsersAsync(gift.InitiatedGiftId.Value, cancellationToken);
        }

        var availableActions = GiftVerificationUiHelper.GetAvailableActions(
            gift,
            userId,
            completedChild,
            initiatedParent);

        var actionName = MapActionName(request.Action);
        if (!availableActions.Contains(actionName))
        {
            return new GiftOperationResponse { Success = false, Message = "This action is not available for this gift." };
        }

        switch (request.Action)
        {
            case GiftVerificationAction.ConfirmReceived:
                await HandleConfirmReceivedAsync(gift, completedChild, initiatedParent, cancellationToken);
                break;
            case GiftVerificationAction.ConfirmNotReceived:
                await HandleConfirmNotReceivedAsync(gift, cancellationToken);
                break;
            case GiftVerificationAction.CompleteTransfer:
                return await HandleCompleteTransferAsync(gift, userId, membership.CrewId, request.PaymentPlatformId, cancellationToken);
            case GiftVerificationAction.CantComplete:
                if (gift.Type != GiftType.Initiated)
                {
                    return new GiftOperationResponse { Success = false, Message = "This action is only valid for middleman gifts." };
                }

                await HandleCantCompleteAsync(gift, completedChild, cancellationToken);
                break;
            default:
                return new GiftOperationResponse { Success = false, Message = "Unknown verification action." };
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await giftRepository.GetByIdWithUsersAsync(gift.Id, cancellationToken);
        return new GiftOperationResponse
        {
            Success = true,
            Message = "Gift verification updated.",
            Entry = saved is not null
                ? GiftMapper.MapGift(saved, viewerUserId: userId, completedChild: completedChild, initiatedParent: initiatedParent)
                : null
        };
    }

    private async Task HandleConfirmReceivedAsync(
        Gift gift,
        Gift? completedChild,
        Gift? initiatedParent,
        CancellationToken cancellationToken)
    {
        switch (gift.Type)
        {
            case GiftType.Direct:
                gift.CountsTowardContribution = true;
                gift.VerificationStatus = GiftVerificationStatus.Verified;
                if (!gift.ReceptionApplied)
                {
                    await mutualAidService.ApplyGiftReceptionAsync(gift, cancellationToken);
                }
                break;

            case GiftType.Initiated:
                gift.CountsTowardContribution = true;
                gift.VerificationStatus = GiftVerificationStatus.MiddlemanReceivedFunds;
                break;

            case GiftType.Completed:
                gift.VerificationStatus = GiftVerificationStatus.Verified;
                if (!gift.ReceptionApplied)
                {
                    await mutualAidService.ApplyGiftReceptionAsync(gift, cancellationToken);
                }
                break;
        }
    }

    private Task HandleConfirmNotReceivedAsync(Gift gift, CancellationToken cancellationToken)
    {
        gift.CountsTowardContribution = false;

        gift.VerificationStatus = gift.Type switch
        {
            GiftType.Direct => GiftVerificationStatus.TransferNotReceived,
            GiftType.Initiated => GiftVerificationStatus.TransferNotReceived,
            GiftType.Completed => GiftVerificationStatus.RecipientNotReceived,
            _ => gift.VerificationStatus
        };

        return Task.CompletedTask;
    }

    private async Task<GiftOperationResponse> HandleCompleteTransferAsync(
        Gift initiated,
        int middlemanUserId,
        int crewId,
        int? paymentPlatformId,
        CancellationToken cancellationToken)
    {
        if (!paymentPlatformId.HasValue || paymentPlatformId.Value <= 0)
        {
            return new GiftOperationResponse { Success = false, Message = "A payment platform is required to complete this gift." };
        }

        if (!await crewPaymentPlatformRepository.ExistsForCrewAsync(crewId, paymentPlatformId.Value, cancellationToken))
        {
            return new GiftOperationResponse { Success = false, Message = "Invalid payment platform." };
        }

        if (initiated.MiddlemanUser is null || initiated.RecipientUser is null)
        {
            return new GiftOperationResponse { Success = false, Message = "Pending gift not found." };
        }

        var commonPlatforms = CrewPaymentPlatformService.GetCommonPlatforms(initiated.MiddlemanUser, initiated.RecipientUser);
        if (!commonPlatforms.Any(p => p.Id == paymentPlatformId.Value))
        {
            return new GiftOperationResponse { Success = false, Message = "Selected payment platform is not shared with the recipient." };
        }

        if (await giftRepository.HasCompletedInitiatedGiftAsync(initiated.Id, cancellationToken))
        {
            return new GiftOperationResponse { Success = false, Message = "This gift has already been completed." };
        }

        var completed = new Gift
        {
            CrewId = crewId,
            GiverUserId = initiated.GiverUserId,
            RecipientUserId = initiated.RecipientUserId,
            MiddlemanUserId = middlemanUserId,
            Type = GiftType.Completed,
            Amount = initiated.Amount,
            CrewPaymentPlatformId = paymentPlatformId.Value,
            InitiatedGiftId = initiated.Id,
            IsSurvivalThreshold = initiated.IsSurvivalThreshold,
            IsCustomGift = initiated.IsCustomGift,
            CountsTowardReception = true,
            CountsTowardContribution = true,
            VerificationStatus = GiftVerificationStatus.AwaitingRecipientVerification,
            CreatedAt = DateTime.UtcNow
        };

        await giftRepository.AddAsync(completed, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await giftRepository.GetByIdWithUsersAsync(completed.Id, cancellationToken);
        return new GiftOperationResponse
        {
            Success = true,
            Message = "Gift transfer completed. Awaiting recipient confirmation.",
            Entry = saved is not null
                ? GiftMapper.MapGift(saved, viewerUserId: middlemanUserId, initiatedParent: initiated)
                : null
        };
    }

    private async Task HandleCantCompleteAsync(
        Gift gift,
        Gift? completedChild,
        CancellationToken cancellationToken)
    {
        if (gift.Type != GiftType.Initiated)
        {
            return;
        }

        gift.VerificationStatus = GiftVerificationStatus.MiddlemanCannotComplete;
        if (!gift.ReceptionApplied && gift.MiddlemanUserId.HasValue)
        {
            await mutualAidService.ApplyGiftReceptionForUserAsync(gift, gift.MiddlemanUserId.Value, cancellationToken);
            gift.ReceptionApplied = true;
        }
    }

    private static string MapActionName(GiftVerificationAction action) => action switch
    {
        GiftVerificationAction.ConfirmReceived => GiftVerificationUiHelper.ActionConfirmReceived,
        GiftVerificationAction.ConfirmNotReceived => GiftVerificationUiHelper.ActionConfirmNotReceived,
        GiftVerificationAction.CompleteTransfer => GiftVerificationUiHelper.ActionCompleteTransfer,
        GiftVerificationAction.CantComplete => GiftVerificationUiHelper.ActionCantComplete,
        _ => string.Empty
    };
}
