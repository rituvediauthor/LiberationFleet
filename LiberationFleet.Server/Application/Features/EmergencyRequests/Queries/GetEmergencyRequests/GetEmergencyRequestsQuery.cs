using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.EmergencyRequests.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.EmergencyRequests.Queries.GetEmergencyRequests;

public record GetEmergencyRequestsQuery : IRequest<EmergencyRequestListResponse>;

public class GetEmergencyRequestsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IEmergencyRequestRepository emergencyRequestRepository) : IRequestHandler<GetEmergencyRequestsQuery, EmergencyRequestListResponse>
{
    public async Task<EmergencyRequestListResponse> Handle(
        GetEmergencyRequestsQuery request,
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

        var requests = await emergencyRequestRepository.GetOpenByCrewIdAsync(membership.CrewId, cancellationToken);
        var items = requests.Select(r => new EmergencyRequestListItemDto
        {
            Id = r.Id,
            RequesterUserId = r.RequesterUserId,
            RequesterUsername = r.RequesterUser.Username,
            PurposePreview = r.Purpose.Length > 120 ? r.Purpose[..117] + "..." : r.Purpose,
            AmountNeeded = r.AmountNeeded,
            AmountFulfilled = r.AmountFulfilled,
            AmountRemaining = Math.Max(0m, r.AmountNeeded - r.AmountFulfilled),
            CreatedAt = r.CreatedAt
        }).ToList();

        return new EmergencyRequestListResponse
        {
            Success = true,
            Message = "Emergency requests loaded.",
            Items = items
        };
    }
}
