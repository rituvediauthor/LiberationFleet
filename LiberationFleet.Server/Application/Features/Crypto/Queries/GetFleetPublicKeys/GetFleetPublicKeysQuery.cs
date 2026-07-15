using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crypto.Queries.GetFleetPublicKeys;

public record GetFleetPublicKeysQuery(int FleetId) : IRequest<IReadOnlyList<UserKeyBundleDto>>;

public class GetFleetPublicKeysQueryHandler(
    ICurrentUserService currentUser,
    IFleetRepository fleetRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetFleetPublicKeysQuery, IReadOnlyList<UserKeyBundleDto>>
{
    public async Task<IReadOnlyList<UserKeyBundleDto>> Handle(GetFleetPublicKeysQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Array.Empty<UserKeyBundleDto>();
        }

        if (!await fleetRepository.IsUserInFleetAsync(currentUser.UserId.Value, request.FleetId, cancellationToken))
        {
            return Array.Empty<UserKeyBundleDto>();
        }

        var userIds = await fleetRepository.GetActiveFleetMemberUserIdsAsync(request.FleetId, cancellationToken);
        var bundles = await cryptoRepository.GetUserKeyBundlesAsync(userIds, cancellationToken);
        return bundles.Select(CryptoMapper.MapKeyBundle).ToList();
    }
}
