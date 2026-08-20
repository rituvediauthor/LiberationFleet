using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.EmergencyRequests;
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

        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken);
        var crewNames = fleetCrews.ToDictionary(fc => fc.CrewId, fc => fc.Crew.Name);

        var items = new List<EmergencyRequestListItemDto>();
        foreach (var fleetCrew in fleetCrews)
        {
            var requests = await emergencyRequestRepository.GetOpenByCrewIdAsync(fleetCrew.CrewId, cancellationToken);
            items.AddRange(requests.Select(r =>
            {
                var amounts = EmergencyRequestDtoMapper.MapAmounts(r);
                return new EmergencyRequestListItemDto
                {
                    Id = r.Id,
                    CrewId = fleetCrew.CrewId,
                    CrewName = crewNames.GetValueOrDefault(fleetCrew.CrewId, string.Empty),
                    RequesterUserId = r.RequesterUserId,
                    RequesterUsername = r.RequesterUser.Username,
                    PurposePreview = r.Purpose.Length > 120 ? r.Purpose[..117] + "..." : r.Purpose,
                    AmountNeeded = r.AmountNeeded,
                    AmountFulfilled = amounts.AmountReceived,
                    AmountReceived = amounts.AmountReceived,
                    AmountSplitCommitted = amounts.AmountSplitCommitted,
                    AmountUncovered = amounts.AmountUncovered,
                    AmountRemaining = amounts.AmountRemaining,
                    CreatedAt = r.CreatedAt
                };
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
