using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Queries.GetPendingMiddlemanGifts;

public record GetPendingMiddlemanGiftsQuery : IRequest<IReadOnlyList<PendingMiddlemanGiftDto>>;

public class GetPendingMiddlemanGiftsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository) : IRequestHandler<GetPendingMiddlemanGiftsQuery, IReadOnlyList<PendingMiddlemanGiftDto>>
{
    public async Task<IReadOnlyList<PendingMiddlemanGiftDto>> Handle(
        GetPendingMiddlemanGiftsQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Array.Empty<PendingMiddlemanGiftDto>();
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return Array.Empty<PendingMiddlemanGiftDto>();
        }

        var gifts = await giftRepository.GetPendingMiddlemanGiftsAsync(
            currentUser.UserId.Value,
            membership.CrewId,
            cancellationToken);

        return gifts.Select(GiftMapper.MapPendingGift).ToList();
    }
}
