using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.EmergencyRequests;
using LiberationFleet.Server.Application.Features.EmergencyRequests.Contracts;
using LiberationFleet.Server.Domain.Entities;
using MediatR;
using LiberationFleet.Server.Application.Common;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetEmergencies;

public record GetFleetEmergenciesQuery : IRequest<EmergencyRequestListResponse>;

public class GetFleetEmergenciesQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IEmergencyRequestRepository emergencyRequestRepository,
    IGiftRepository giftRepository) : IRequestHandler<GetFleetEmergenciesQuery, EmergencyRequestListResponse>
{
    public async Task<EmergencyRequestListResponse> Handle(
        GetFleetEmergenciesQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new EmergencyRequestListResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return new EmergencyRequestListResponse { Success = false, Message = "You are not in a crew." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new EmergencyRequestListResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken);
        var crewNames = fleetCrews.ToDictionary(fc => fc.CrewId, fc => fc.Crew.Name);
        var crewIds = fleetCrews.Select(fc => fc.CrewId).ToList();
        var openRequests = await emergencyRequestRepository.GetOpenByCrewIdsAsync(crewIds, cancellationToken);
        var activeUserIdsByCrew = await membershipRepository.GetActiveNonPlaceholderUserIdsByCrewIdsAsync(
            crewIds,
            cancellationToken);
        var requestsByCrew = openRequests
            .GroupBy(request => request.CrewId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var visible = new List<EmergencyRequest>();
        foreach (var crewId in crewIds)
        {
            if (!requestsByCrew.TryGetValue(crewId, out var requests))
            {
                continue;
            }

            var activeUserIds = activeUserIdsByCrew.GetValueOrDefault(crewId);
            if (activeUserIds is null)
            {
                continue;
            }

            visible.AddRange(requests.Where(request => activeUserIds.Contains(request.RequesterUserId)));
        }
        var pendingByRequest = await giftRepository.GetPendingAmountsByEmergencyRequestIdsAsync(
            visible.Select(request => request.Id),
            cancellationToken);

        var items = visible.Select(request =>
        {
            pendingByRequest.TryGetValue(request.Id, out var pending);
            var amounts = EmergencyRequestDtoMapper.MapAmounts(request, pending);
            return new EmergencyRequestListItemDto
            {
                Id = request.Id,
                CrewId = request.CrewId,
                CrewName = crewNames.GetValueOrDefault(request.CrewId, string.Empty),
                RequesterUserId = request.RequesterUserId,
                RequesterUsername = UserDisplay.Name(request.RequesterUser),
                PurposePreview = request.Purpose.Length > 120 ? request.Purpose[..117] + "..." : request.Purpose,
                AmountNeeded = request.AmountNeeded,
                AmountFulfilled = amounts.AmountReceived,
                AmountReceived = amounts.AmountReceived,
                AmountSplitCommitted = amounts.AmountSplitCommitted,
                AmountUncovered = amounts.AmountUncovered,
                AmountRemaining = amounts.AmountRemaining,
                CreatedAt = request.CreatedAt
            };
        }).ToList();

        items = items.OrderByDescending(i => i.CreatedAt).ToList();

        return new EmergencyRequestListResponse
        {
            Success = true,
            Message = "Fleet emergencies loaded.",
            Items = items
        };
    }
}
