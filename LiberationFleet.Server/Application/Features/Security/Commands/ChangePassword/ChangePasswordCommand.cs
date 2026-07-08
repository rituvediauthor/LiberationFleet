using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Security.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Security.Commands.ChangePassword;

public record ChangePasswordCommand(ChangePasswordRequest Request) : IRequest<SecurityOperationResponse>;

public class ChangePasswordCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ISecurityRepository securityRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ChangePasswordCommand, SecurityOperationResponse>
{
    public async Task<SecurityOperationResponse> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new SecurityOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var body = request.Request;
        if (string.IsNullOrWhiteSpace(body.CurrentPassword)
            || string.IsNullOrWhiteSpace(body.NewPassword)
            || string.IsNullOrWhiteSpace(body.ConfirmPassword))
        {
            return new SecurityOperationResponse { Success = false, Message = "All password fields are required." };
        }

        if (body.NewPassword != body.ConfirmPassword)
        {
            return new SecurityOperationResponse { Success = false, Message = "New passwords do not match." };
        }

        if (body.NewPassword.Length < 8)
        {
            return new SecurityOperationResponse { Success = false, Message = "New password must be at least 8 characters." };
        }

        var user = await userRepository.GetByIdWithProfileAsync(currentUser.UserId.Value, cancellationToken);
        if (user is null)
        {
            return new SecurityOperationResponse { Success = false, Message = "User not found." };
        }

        if (!passwordHasher.Verify(body.CurrentPassword, user.PasswordHash))
        {
            return new SecurityOperationResponse { Success = false, Message = "Current password is incorrect." };
        }

        user.PasswordHash = passwordHasher.Hash(body.NewPassword);
        user.FailedLoginAttempts = 0;
        user.LastFailedLoginAt = null;
        await userRepository.UpdateAsync(user, cancellationToken);

        await SettingsLockHelper.RecordSettingsChangedAlertAsync(
            user.Id,
            "Password",
            securityRepository,
            unitOfWork,
            cancellationToken);

        return new SecurityOperationResponse { Success = true, Message = "Password updated successfully." };
    }
}
