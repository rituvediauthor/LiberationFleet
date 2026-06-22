using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Domain.Entities;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crypto.Commands.UpsertPublicKey;

public record UpsertPublicKeyCommand(string IdentityPublicKey, int KeyVersion = 1) : IRequest<CryptoOperationResponse>;

public class UpsertPublicKeyCommandHandler(
    ICurrentUserService currentUser,
    ICryptoRepository cryptoRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpsertPublicKeyCommand, CryptoOperationResponse>
{
    public async Task<CryptoOperationResponse> Handle(UpsertPublicKeyCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CryptoOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.IdentityPublicKey))
        {
            return new CryptoOperationResponse { Success = false, Message = "Public key is required." };
        }

        var userId = currentUser.UserId.Value;
        await cryptoRepository.UpsertUserKeyBundleAsync(new UserKeyBundle
        {
            UserId = userId,
            IdentityPublicKey = request.IdentityPublicKey.Trim(),
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            UpdatedAt = DateTime.UtcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CryptoOperationResponse { Success = true, Message = "Public key saved." };
    }
}
