using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Security.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Security.Queries.GetSecuritySettings;

public record GetSecuritySettingsQuery : IRequest<SecuritySettingsResponse>;

public class GetSecuritySettingsQueryHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository) : IRequestHandler<GetSecuritySettingsQuery, SecuritySettingsResponse>
{
    public async Task<SecuritySettingsResponse> Handle(GetSecuritySettingsQuery request, CancellationToken cancellationToken)
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

        return new SecuritySettingsResponse
        {
            Success = true,
            Message = "Security settings loaded.",
            Settings = new SecuritySettingsDto
            {
                // Never advertise MFA as on until login enforces a challenge.
                TwoFactorEnabled = false,
                MfaAvailable = false,
                LockSettingsWithPassword = user.LockSettingsWithPassword,
                HasSettingsLockPassword = !string.IsNullOrWhiteSpace(user.SettingsLockPasswordHash)
            }
        };
    }
}
