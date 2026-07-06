using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Queries.GetMyGiftHistoryForRecipient;

public record GetMyGiftHistoryForRecipientQuery(int RecipientUserId) : IRequest<GiftHistoryDetailResponse>;

public class GetMyGiftHistoryForRecipientQueryHandler(
    ICurrentUserService currentUser,
    IGiftRepository giftRepository,
    IUserRepository userRepository) : IRequestHandler<GetMyGiftHistoryForRecipientQuery, GiftHistoryDetailResponse>
{
    public async Task<GiftHistoryDetailResponse> Handle(GetMyGiftHistoryForRecipientQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new GiftHistoryDetailResponse { Success = false, Message = "Unauthorized." };
        }

        var recipient = await userRepository.GetByIdWithProfileAsync(request.RecipientUserId, cancellationToken);
        if (recipient is null)
        {
            return new GiftHistoryDetailResponse { Success = false, Message = "Recipient not found." };
        }

        var gifts = await giftRepository.GetGiftsByGiverAndRecipientAsync(
            currentUser.UserId.Value,
            request.RecipientUserId,
            cancellationToken);

        if (gifts.Count == 0)
        {
            return new GiftHistoryDetailResponse { Success = false, Message = "No gift history found for this person." };
        }

        return new GiftHistoryDetailResponse
        {
            Success = true,
            Message = "Gift history loaded.",
            RecipientUserId = recipient.Id,
            RecipientUsername = recipient.Username,
            TotalAmount = gifts.Sum(g => g.Amount),
            Items = gifts.Select(gift => new GiftHistoryEntryDto
            {
                Id = gift.Id,
                Amount = gift.Amount,
                Timestamp = gift.CreatedAt
            }).ToList()
        };
    }
}
