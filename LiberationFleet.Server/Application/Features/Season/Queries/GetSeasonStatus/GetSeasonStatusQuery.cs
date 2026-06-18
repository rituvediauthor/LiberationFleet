using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Season.Queries.GetSeasonStatus;

public record GetSeasonStatusQuery : IRequest<SeasonStatusDto>;

public class GetSeasonStatusQueryHandler(
    ICurrentUserService currentUser,
    IMutualAidService mutualAidService) : IRequestHandler<GetSeasonStatusQuery, SeasonStatusDto>
{
    public async Task<SeasonStatusDto> Handle(GetSeasonStatusQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new SeasonStatusDto();
        }

        return await mutualAidService.GetSeasonStatusAsync(currentUser.UserId.Value, cancellationToken);
    }
}
