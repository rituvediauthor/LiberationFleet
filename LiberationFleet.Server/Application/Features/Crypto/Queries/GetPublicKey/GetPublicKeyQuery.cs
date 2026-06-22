using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crypto.Queries.GetPublicKey;

public record GetPublicKeyQuery(int UserId) : IRequest<UserKeyBundleDto?>;

public class GetPublicKeyQueryHandler(
    ICurrentUserService currentUser,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetPublicKeyQuery, UserKeyBundleDto?>
{
    public async Task<UserKeyBundleDto?> Handle(GetPublicKeyQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return null;
        }

        var bundle = await cryptoRepository.GetUserKeyBundleAsync(request.UserId, cancellationToken);
        return bundle is null ? null : CryptoMapper.MapKeyBundle(bundle);
    }
}
