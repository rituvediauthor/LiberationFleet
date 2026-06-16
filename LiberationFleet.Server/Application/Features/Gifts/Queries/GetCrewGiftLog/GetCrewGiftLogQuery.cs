using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Queries.GetCrewGiftLog;

public record GetCrewGiftLogQuery : IRequest<GiftLogResponse>;

public class GetCrewGiftLogQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository) : IRequestHandler<GetCrewGiftLogQuery, GiftLogResponse>
{
    public async Task<GiftLogResponse> Handle(GetCrewGiftLogQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new GiftLogResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return new GiftLogResponse { Success = false, Message = "You are not in a crew." };
        }

        var gifts = await giftRepository.GetLogByCrewIdAsync(membership.CrewId, cancellationToken);
        var items = gifts.Select(GiftMapper.MapGift).ToList();

        return new GiftLogResponse
        {
            Success = true,
            Message = "Gift log loaded.",
            Items = items
        };
    }
}
