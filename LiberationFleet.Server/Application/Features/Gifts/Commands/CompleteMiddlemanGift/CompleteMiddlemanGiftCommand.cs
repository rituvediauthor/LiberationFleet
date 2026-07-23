using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Commands.CompleteMiddlemanGift;

public record CompleteMiddlemanGiftRequest(int PaymentPlatformId);

public record CompleteMiddlemanGiftCommand(int GiftId, int PaymentPlatformId) : IRequest<GiftOperationResponse>;

public class CompleteMiddlemanGiftCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CompleteMiddlemanGiftCommand, GiftOperationResponse>
{
    public async Task<GiftOperationResponse> Handle(CompleteMiddlemanGiftCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new GiftOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (request.PaymentPlatformId <= 0)
        {
            return new GiftOperationResponse { Success = false, Message = "A payment platform is required to complete this gift." };
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

        var initiated = await giftRepository.GetByIdWithUsersAsync(request.GiftId, cancellationToken);
        if (initiated is null || initiated.CrewId != membership.CrewId || initiated.Type != GiftType.Initiated)
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
        if (!commonPlatforms.Any(p => p.Id == request.PaymentPlatformId))
        {
            return new GiftOperationResponse { Success = false, Message = "Selected payment platform is not shared with the recipient." };
        }

        if (initiated.VerificationStatus != GiftVerificationStatus.MiddlemanReceivedFunds)
        {
            return new GiftOperationResponse { Success = false, Message = "Confirm that you received the funds before completing this gift." };
        }

        var gift = new Gift
        {
            CrewId = membership.CrewId,
            GiverUserId = initiated.GiverUserId,
            RecipientUserId = initiated.RecipientUserId,
            MiddlemanUserId = userId,
            Type = GiftType.Completed,
            Amount = initiated.Amount,
            CrewPaymentPlatformId = request.PaymentPlatformId,
            InitiatedGiftId = initiated.Id,
            IsSurvivalThreshold = initiated.IsSurvivalThreshold,
            IsRepresentativeGift = initiated.IsRepresentativeGift,
            IsCustomGift = initiated.IsCustomGift,
            CountsTowardReception = true,
            CountsTowardContribution = true,
            SeasonCycleId = initiated.SeasonCycleId,
            VerificationStatus = GiftVerificationStatus.AwaitingRecipientVerification,
            CreatedAt = DateTime.UtcNow
        };

        await giftRepository.AddAsync(gift, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await giftRepository.GetByIdWithUsersAsync(gift.Id, cancellationToken);
        return new GiftOperationResponse
        {
            Success = true,
            Message = "Gift completed. Awaiting recipient confirmation.",
            Entry = saved is not null ? GiftMapper.MapGift(saved, viewerUserId: userId, initiatedParent: initiated) : null
        };
    }
}
