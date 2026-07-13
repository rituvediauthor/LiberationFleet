using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetMyFleetJoinRequests;

public record GetMyFleetJoinRequestsQuery : IRequest<FleetJoinRequestListResponse>;

public class GetMyFleetJoinRequestsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IProposalRepository proposalRepository,
    IFleetRepository fleetRepository) : IRequestHandler<GetMyFleetJoinRequestsQuery, FleetJoinRequestListResponse>
{
    public async Task<FleetJoinRequestListResponse> Handle(
        GetMyFleetJoinRequestsQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FleetJoinRequestListResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return new FleetJoinRequestListResponse { Success = false, Message = "You are not in a crew." };
        }

        var proposals = await proposalRepository.GetPendingCrewApplyToFleetProposalsByCrewAsync(
            membership.CrewId,
            cancellationToken);
        var sideMap = await proposalRepository.GetCrewApplyToFleetsByProposalIdsAsync(
            proposals.Select(p => p.Id),
            cancellationToken);

        var items = new List<FleetJoinRequestListItemDto>();
        foreach (var proposal in proposals.Where(p => p.Status == ProposalStatus.Pending))
        {
            if (!sideMap.TryGetValue(proposal.Id, out var apply))
            {
                continue;
            }

            var fleet = await fleetRepository.GetByIdAsync(apply.FleetId, cancellationToken);
            items.Add(new FleetJoinRequestListItemDto
            {
                ProposalId = proposal.Id,
                FleetId = apply.FleetId,
                FleetName = fleet?.Name ?? "Unknown fleet",
                Status = proposal.Status.ToString(),
                ApproveCount = proposal.ApproveCount,
                DisapproveCount = proposal.DisapproveCount,
                ApprovalTimerEndsAt = proposal.ApprovalTimerEndsAt,
                CreatedAt = proposal.CreatedAt
            });
        }

        return new FleetJoinRequestListResponse
        {
            Success = true,
            Message = items.Count > 0 ? "Join requests loaded." : "No pending fleet join requests.",
            Items = items
        };
    }
}
