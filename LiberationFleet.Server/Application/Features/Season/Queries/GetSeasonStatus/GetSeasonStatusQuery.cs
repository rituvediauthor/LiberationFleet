using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Season.Queries.GetSeasonStatus;

public record GetSeasonStatusQuery : IRequest<SeasonStatusDto>;

public class GetSeasonStatusQueryHandler(
    ICurrentUserService currentUser,
    IMutualAidService mutualAidService,
    ICrewMembershipRepository membershipRepository,
    IProposalRepository proposalRepository) : IRequestHandler<GetSeasonStatusQuery, SeasonStatusDto>
{
    public async Task<SeasonStatusDto> Handle(GetSeasonStatusQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new SeasonStatusDto();
        }

        var status = await mutualAidService.GetSeasonStatusAsync(currentUser.UserId.Value, cancellationToken);
        if (status.SeasonStarted)
        {
            return status;
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(
            currentUser.UserId.Value,
            cancellationToken);
        if (membership is null)
        {
            return status;
        }

        var pending = await proposalRepository.GetPendingCrewStartSeasonAsync(
            membership.CrewId,
            cancellationToken);
        if (pending is not null)
        {
            status.HasPendingStartSeasonProposal = true;
            status.PendingStartSeasonProposalId = pending.ProposalId;
            status.CanStartSeason = false;
        }

        return status;
    }
}
