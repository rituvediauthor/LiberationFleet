using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Domain.Entities;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crypto.Commands.UpsertPrivateKeyBackup;

public record UpsertPrivateKeyBackupCommand(
    string Salt,
    string Iv,
    string Ciphertext,
    int KeyVersion = 1) : IRequest<CryptoOperationResponse>;

public class UpsertPrivateKeyBackupCommandHandler(
    ICurrentUserService currentUser,
    ICryptoRepository cryptoRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpsertPrivateKeyBackupCommand, CryptoOperationResponse>
{
    public async Task<CryptoOperationResponse> Handle(UpsertPrivateKeyBackupCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CryptoOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Salt)
            || string.IsNullOrWhiteSpace(request.Iv)
            || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new CryptoOperationResponse { Success = false, Message = "Encrypted backup payload is required." };
        }

        var userId = currentUser.UserId.Value;
        await cryptoRepository.UpsertPrivateKeyBackupAsync(new UserPrivateKeyBackup
        {
            UserId = userId,
            Salt = request.Salt.Trim(),
            Iv = request.Iv.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            UpdatedAt = DateTime.UtcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CryptoOperationResponse { Success = true, Message = "Private key backup saved." };
    }
}
