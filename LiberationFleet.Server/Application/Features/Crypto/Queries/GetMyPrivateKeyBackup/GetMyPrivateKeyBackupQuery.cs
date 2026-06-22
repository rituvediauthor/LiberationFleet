using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crypto.Queries.GetMyPrivateKeyBackup;

public record GetMyPrivateKeyBackupQuery() : IRequest<UserPrivateKeyBackupDto?>;

public class GetMyPrivateKeyBackupQueryHandler(
    ICurrentUserService currentUser,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetMyPrivateKeyBackupQuery, UserPrivateKeyBackupDto?>
{
    public async Task<UserPrivateKeyBackupDto?> Handle(GetMyPrivateKeyBackupQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return null;
        }

        var backup = await cryptoRepository.GetPrivateKeyBackupAsync(currentUser.UserId.Value, cancellationToken);
        return backup is null ? null : CryptoMapper.MapPrivateKeyBackup(backup);
    }
}
