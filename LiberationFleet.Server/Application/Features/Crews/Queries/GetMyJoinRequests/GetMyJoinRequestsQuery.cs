using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Queries.GetMyJoinRequests;

public record GetMyJoinRequestsQuery : IRequest<JoinRequestListResponse>;

public class GetMyJoinRequestsQueryHandler(
    ICurrentUserService currentUser,
    IProposalRepository proposalRepository,
    ICrewRepository crewRepository) : IRequestHandler<GetMyJoinRequestsQuery, JoinRequestListResponse>
{
    public async Task<JoinRequestListResponse> Handle(GetMyJoinRequestsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new JoinRequestListResponse { Success = false, Message = "Unauthorized." };
        }

        var proposals = await proposalRepository.GetJoinRequestProposalsByApplicantAsync(
            currentUser.UserId.Value,
            cancellationToken);

        var joinRequestIds = proposals.Select(p => p.Id).ToList();
        var joinRequests = await proposalRepository.GetCrewJoinRequestsByProposalIdsAsync(joinRequestIds, cancellationToken);

        var items = new List<JoinRequestListItemDto>();
        foreach (var proposal in proposals.Where(p => p.Status == ProposalStatus.Pending))
        {
            joinRequests.TryGetValue(proposal.Id, out var joinRequest);
            var crew = await crewRepository.GetByIdAsync(proposal.CrewId!.Value, cancellationToken);

            items.Add(new JoinRequestListItemDto
            {
                ProposalId = proposal.Id,
                CrewId = proposal.CrewId!.Value,
                CrewName = crew?.Name ?? "Unknown crew",
                Status = proposal.Status.ToString(),
                ApproveCount = proposal.ApproveCount,
                DisapproveCount = proposal.DisapproveCount,
                ApprovalTimerEndsAt = proposal.ApprovalTimerEndsAt,
                IsKeyPrepared = joinRequest?.IsKeyPrepared ?? false,
                CreatedAt = proposal.CreatedAt
            });
        }

        return new JoinRequestListResponse
        {
            Success = true,
            Message = items.Count > 0 ? "Join requests loaded." : "No pending join requests.",
            Items = items
        };
    }
}
