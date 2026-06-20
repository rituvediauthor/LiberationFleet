using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Queries.GetReceptionOrder;

public record GetReceptionOrderQuery(int Limit = 30) : IRequest<IReadOnlyList<ReceptionOrderEntryDto>>;

public class GetReceptionOrderQueryHandler(
    ICurrentUserService currentUser,
    IMutualAidService mutualAidService) : IRequestHandler<GetReceptionOrderQuery, IReadOnlyList<ReceptionOrderEntryDto>>
{
    public async Task<IReadOnlyList<ReceptionOrderEntryDto>> Handle(GetReceptionOrderQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Array.Empty<ReceptionOrderEntryDto>();
        }

        return await mutualAidService.GetReceptionOrderAsync(
            currentUser.UserId.Value,
            request.Limit,
            requireGiverInSeason: true,
            excludeSelfAsRecipient: true,
            cancellationToken);
    }
}
