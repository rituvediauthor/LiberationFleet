using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Queries.GetNextAid;

public record GetNextAidQuery : IRequest<NextAidDto?>;

public class GetNextAidQueryHandler(
    ICurrentUserService currentUser,
    IMutualAidService mutualAidService) : IRequestHandler<GetNextAidQuery, NextAidDto?>
{
    public async Task<NextAidDto?> Handle(GetNextAidQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return null;
        }

        return await mutualAidService.GetNextAidAsync(currentUser.UserId.Value, cancellationToken);
    }
}
