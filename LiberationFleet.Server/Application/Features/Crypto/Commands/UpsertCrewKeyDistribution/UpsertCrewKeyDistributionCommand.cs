using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Domain.Entities;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crypto.Commands.UpsertCrewKeyDistribution;

public record UpsertCrewKeyDistributionCommand(
    int CrewId,
    int UserId,
    int KeyVersion,
    string WrappedCrewKey,
    string WrapNonce) : IRequest<CryptoOperationResponse>;

public class UpsertCrewKeyDistributionCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICryptoRepository cryptoRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpsertCrewKeyDistributionCommand, CryptoOperationResponse>
{
    public async Task<CryptoOperationResponse> Handle(UpsertCrewKeyDistributionCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CryptoOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var actorId = currentUser.UserId.Value;
        if (!await membershipRepository.IsUserInCrewAsync(actorId, request.CrewId, cancellationToken))
        {
            return new CryptoOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(request.UserId, request.CrewId, cancellationToken))
        {
            return new CryptoOperationResponse { Success = false, Message = "Target user is not in this crew." };
        }

        if (string.IsNullOrWhiteSpace(request.WrappedCrewKey) || string.IsNullOrWhiteSpace(request.WrapNonce))
        {
            return new CryptoOperationResponse { Success = false, Message = "Wrapped crew key payload is required." };
        }

        await cryptoRepository.UpsertCrewKeyDistributionAsync(new CrewKeyDistribution
        {
            CrewId = request.CrewId,
            UserId = request.UserId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            WrappedCrewKey = request.WrappedCrewKey.Trim(),
            WrapNonce = request.WrapNonce.Trim(),
            WrappedByUserId = actorId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CryptoOperationResponse { Success = true, Message = "Crew key distribution saved." };
    }
}
