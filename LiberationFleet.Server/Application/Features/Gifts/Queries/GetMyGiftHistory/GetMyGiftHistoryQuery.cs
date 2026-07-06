using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Queries.GetMyGiftHistory;

public record GetMyGiftHistoryQuery : IRequest<GiftHistoryRecipientListResponse>;

public class GetMyGiftHistoryQueryHandler(
    ICurrentUserService currentUser,
    IGiftRepository giftRepository) : IRequestHandler<GetMyGiftHistoryQuery, GiftHistoryRecipientListResponse>
{
    public async Task<GiftHistoryRecipientListResponse> Handle(GetMyGiftHistoryQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new GiftHistoryRecipientListResponse { Success = false, Message = "Unauthorized." };
        }

        var summaries = await giftRepository.GetGiverRecipientSummariesAsync(currentUser.UserId.Value, cancellationToken);

        return new GiftHistoryRecipientListResponse
        {
            Success = true,
            Message = "Gift history loaded.",
            Items = summaries.Select(summary => new GiftHistoryRecipientSummaryDto
            {
                RecipientUserId = summary.RecipientUserId,
                RecipientUsername = summary.RecipientUsername,
                TotalAmount = summary.TotalAmount
            }).ToList()
        };
    }
}
