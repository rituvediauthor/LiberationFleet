using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crypto.Commands.UpsertFleetKeyDistribution;

public record UpsertFleetKeyDistributionCommand(
    int FleetId,
    int UserId,
    int KeyVersion,
    string WrappedFleetKey,
    string WrapNonce) : IRequest<CryptoOperationResponse>;

public class UpsertFleetKeyDistributionCommandHandler(
    ICurrentUserService currentUser,
    IFleetRepository fleetRepository,
    ICryptoRepository cryptoRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpsertFleetKeyDistributionCommand, CryptoOperationResponse>
{
    public async Task<CryptoOperationResponse> Handle(UpsertFleetKeyDistributionCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CryptoOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var actorId = currentUser.UserId.Value;
        if (!await fleetRepository.IsUserInFleetAsync(actorId, request.FleetId, cancellationToken))
        {
            return new CryptoOperationResponse { Success = false, Message = "You are not in this fleet." };
        }

        if (!await fleetRepository.IsUserInFleetAsync(request.UserId, request.FleetId, cancellationToken))
        {
            return new CryptoOperationResponse { Success = false, Message = "Target user is not in this fleet." };
        }

        if (string.IsNullOrWhiteSpace(request.WrappedFleetKey) || string.IsNullOrWhiteSpace(request.WrapNonce))
        {
            return new CryptoOperationResponse { Success = false, Message = "Wrapped fleet key payload is required." };
        }

        await cryptoRepository.UpsertFleetKeyDistributionAsync(new Domain.Entities.FleetKeyDistribution
        {
            FleetId = request.FleetId,
            UserId = request.UserId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            WrappedFleetKey = request.WrappedFleetKey.Trim(),
            WrapNonce = request.WrapNonce.Trim(),
            WrappedByUserId = actorId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CryptoOperationResponse { Success = true, Message = "Fleet key distribution saved." };
    }
}
