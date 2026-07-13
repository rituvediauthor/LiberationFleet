using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetGiftLog;

public record GetFleetGiftLogQuery(
    int Limit = 50,
    DateTime? BeforeCreatedAt = null,
    int? BeforeId = null) : IRequest<GiftLogResponse>;

public class GetFleetGiftLogQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IGiftRepository giftRepository) : IRequestHandler<GetFleetGiftLogQuery, GiftLogResponse>
{
    public async Task<GiftLogResponse> Handle(GetFleetGiftLogQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new GiftLogResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new GiftLogResponse { Success = false, Message = "You are not in a crew." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new GiftLogResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        var crewIds = (await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken))
            .Select(fc => fc.CrewId)
            .ToList();

        var limit = request.Limit <= 0 ? 50 : Math.Min(request.Limit, 100);
        var page = await giftRepository.GetLogPageByCrewIdsAsync(
            crewIds,
            limit,
            request.BeforeCreatedAt,
            request.BeforeId,
            cancellationToken);

        var items = page.Items.Select(gift => GiftMapper.MapGift(gift, userId)).ToList();

        return new GiftLogResponse
        {
            Success = true,
            Message = "Fleet gift log loaded.",
            Items = items,
            HasMore = page.HasMore
        };
    }
}
