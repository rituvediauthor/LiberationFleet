using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Security;
using LiberationFleet.Server.Application.Features.Security.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Security.Commands.UpdateSecuritySettings;

public record UpdateSecuritySettingsCommand(UpdateSecuritySettingsRequest Request)
    : IRequest<SecuritySettingsResponse>;

public class UpdateSecuritySettingsCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateSecuritySettingsCommand, SecuritySettingsResponse>
{
    public async Task<SecuritySettingsResponse> Handle(UpdateSecuritySettingsCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new SecuritySettingsResponse { Success = false, Message = "Unauthorized." };
        }

        var user = await userRepository.GetByIdWithProfileAsync(currentUser.UserId.Value, cancellationToken);
        if (user is null)
        {
            return new SecuritySettingsResponse { Success = false, Message = "User not found." };
        }

        var body = request.Request;
        var lockCheck = await SettingsLockHelper.VerifySettingsPasswordAsync(user, body.SettingsPassword, passwordHasher);
        if (!lockCheck.Allowed)
        {
            return new SecuritySettingsResponse { Success = false, Message = lockCheck.Message };
        }

        // MFA is not implemented yet (no login challenge). Reject enable attempts so
        // clients cannot persist a false sense of security. Clear any legacy flag.
        if (body.TwoFactorEnabled == true)
        {
            return new SecuritySettingsResponse
            {
                Success = false,
                Message = "Two-factor authentication is not available yet."
            };
        }

        if (user.TwoFactorEnabled)
        {
            user.TwoFactorEnabled = false;
        }

        if (body.LockSettingsWithPassword.HasValue)
        {
            if (body.LockSettingsWithPassword.Value)
            {
                if (!string.IsNullOrWhiteSpace(body.NewSettingsLockPassword))
                {
                    user.SettingsLockPasswordHash = passwordHasher.Hash(body.NewSettingsLockPassword);
                }
                else if (string.IsNullOrWhiteSpace(user.SettingsLockPasswordHash))
                {
                    return new SecuritySettingsResponse
                    {
                        Success = false,
                        Message = "Set a settings lock password before enabling lock."
                    };
                }
            }
            else if (!string.IsNullOrWhiteSpace(user.SettingsLockPasswordHash))
            {
                if (string.IsNullOrWhiteSpace(body.CurrentSettingsLockPassword)
                    || !passwordHasher.Verify(body.CurrentSettingsLockPassword, user.SettingsLockPasswordHash))
                {
                    return new SecuritySettingsResponse
                    {
                        Success = false,
                        Message = "Enter your current settings lock password to disable locking."
                    };
                }

                user.SettingsLockPasswordHash = null;
            }

            user.LockSettingsWithPassword = body.LockSettingsWithPassword.Value;
        }

        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SecuritySettingsResponse
        {
            Success = true,
            Message = "Security settings saved.",
            Settings = new SecuritySettingsDto
            {
                TwoFactorEnabled = false,
                MfaAvailable = false,
                LockSettingsWithPassword = user.LockSettingsWithPassword,
                HasSettingsLockPassword = !string.IsNullOrWhiteSpace(user.SettingsLockPasswordHash)
            }
        };
    }
}
