using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Commands.CompleteMiddlemanGift;

public record CompleteMiddlemanGiftCommand(int GiftId) : IRequest<GiftOperationResponse>;

public class CompleteMiddlemanGiftCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    IMutualAidService mutualAidService,
    IUnitOfWork unitOfWork) : IRequestHandler<CompleteMiddlemanGiftCommand, GiftOperationResponse>
{
    public async Task<GiftOperationResponse> Handle(CompleteMiddlemanGiftCommand request, CancellationToken cancellationToken)
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

        var gift = new Domain.Entities.Gift
        {
            CrewId = membership.CrewId,
            GiverUserId = initiated.GiverUserId,
            RecipientUserId = initiated.RecipientUserId,
            MiddlemanUserId = userId,
            Type = GiftType.Completed,
            Amount = initiated.Amount,
            PaymentPlatformId = initiated.PaymentPlatformId,
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
            Entry = saved is not null ? GiftMapper.MapGift(saved, canCompleteAsMiddleman: false, status: "completed") : null
        };
    }
}
