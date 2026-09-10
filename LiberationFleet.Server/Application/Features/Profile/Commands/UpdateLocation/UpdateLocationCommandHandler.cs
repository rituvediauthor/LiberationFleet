using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Profile.Commands.UpdateLocation;

public class UpdateLocationCommandHandler : IRequestHandler<UpdateLocationCommand, ProfileOperationResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateLocationCommandHandler(
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProfileOperationResponse> Handle(UpdateLocationCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
        {
            return new ProfileOperationResponse { Success = false, Message = "Unauthorized" };
        }

        var user = await _userRepository.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return new ProfileOperationResponse { Success = false, Message = "User not found" };
        }

        if (request.ClearLocation || request.EncryptedLocation is null)
        {
            user.LocationNonce = null;
            user.LocationCiphertext = null;
            user.LocationKeyVersion = null;
        }
        else
        {
            user.LocationNonce = request.EncryptedLocation.Nonce.Trim();
            user.LocationCiphertext = request.EncryptedLocation.Ciphertext.Trim();
            user.LocationKeyVersion = request.EncryptedLocation.KeyVersion;
        }

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProfileOperationResponse
        {
            Success = true,
            Message = "Location updated"
        };
    }
}
