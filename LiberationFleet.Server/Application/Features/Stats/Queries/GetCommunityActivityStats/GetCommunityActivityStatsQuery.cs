using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Stats.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Stats.Queries.GetCommunityActivityStats;

public record GetCommunityActivityStatsQuery : IRequest<CommunityActivityStatsResponse>;

public sealed class GetCommunityActivityStatsQueryHandler(ICrewMembershipRepository membershipRepository)
    : IRequestHandler<GetCommunityActivityStatsQuery, CommunityActivityStatsResponse>
{
    public const int ActiveWindowDays = 30;

    public async Task<CommunityActivityStatsResponse> Handle(
        GetCommunityActivityStatsQuery request,
        CancellationToken cancellationToken)
    {
        var activeSinceUtc = DateTime.UtcNow.AddDays(-ActiveWindowDays);
        var (activeCrewCount, activeCrewmateCount) =
            await membershipRepository.CountRecentlyActiveCommunityAsync(activeSinceUtc, cancellationToken);

        return new CommunityActivityStatsResponse
        {
            ActiveCrewCount = activeCrewCount,
            ActiveCrewmateCount = activeCrewmateCount,
            ActiveWindowDays = ActiveWindowDays
        };
    }
}
