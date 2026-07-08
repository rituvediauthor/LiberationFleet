using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Security.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Security.Commands.VerifySettingsPassword;

public record VerifySettingsPasswordCommand(string SettingsPassword) : IRequest<VerifySettingsPasswordResponse>;

public class VerifySettingsPasswordCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher) : IRequestHandler<VerifySettingsPasswordCommand, VerifySettingsPasswordResponse>
{
    public async Task<VerifySettingsPasswordResponse> Handle(VerifySettingsPasswordCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new VerifySettingsPasswordResponse { Success = false, Message = "Unauthorized." };
        }

        var user = await userRepository.GetByIdWithProfileAsync(currentUser.UserId.Value, cancellationToken);
        if (user is null)
        {
            return new VerifySettingsPasswordResponse { Success = false, Message = "User not found." };
        }

        var check = await SettingsLockHelper.VerifySettingsPasswordAsync(user, request.SettingsPassword, passwordHasher);
        return new VerifySettingsPasswordResponse
        {
            Success = check.Allowed,
            Message = check.Allowed ? "Settings password verified." : check.Message
        };
    }
}
