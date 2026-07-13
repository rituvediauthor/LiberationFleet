using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.EmergencyRequests.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetEmergencies;

public record GetFleetEmergenciesQuery : IRequest<EmergencyRequestListResponse>;

public class GetFleetEmergenciesQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IEmergencyRequestRepository emergencyRequestRepository) : IRequestHandler<GetFleetEmergenciesQuery, EmergencyRequestListResponse>
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

        var crewIds = (await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken))
            .Select(fc => fc.CrewId)
            .ToList();

        var items = new List<EmergencyRequestListItemDto>();
        foreach (var crewId in crewIds)
        {
            var requests = await emergencyRequestRepository.GetOpenByCrewIdAsync(crewId, cancellationToken);
            items.AddRange(requests.Select(r => new EmergencyRequestListItemDto
            {
                Id = r.Id,
                RequesterUserId = r.RequesterUserId,
                RequesterUsername = r.RequesterUser.Username,
                PurposePreview = r.Purpose.Length > 120 ? r.Purpose[..117] + "..." : r.Purpose,
                AmountNeeded = r.AmountNeeded,
                AmountFulfilled = r.AmountFulfilled,
                AmountRemaining = Math.Max(0m, r.AmountNeeded - r.AmountFulfilled),
                CreatedAt = r.CreatedAt
            }));
        }

        items = items.OrderByDescending(i => i.CreatedAt).ToList();

        return new EmergencyRequestListResponse
        {
            Success = true,
            Message = "Fleet emergencies loaded.",
            Items = items
        };
    }
}
