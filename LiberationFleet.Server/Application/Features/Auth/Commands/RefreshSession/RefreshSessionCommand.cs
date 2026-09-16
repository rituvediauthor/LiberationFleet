using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Auth.Contracts;
using LiberationFleet.Server.Application.Features.Security;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace LiberationFleet.Server.Application.Features.Auth.Commands.RefreshSession;

public record RefreshSessionCommand : IRequest<LoginResponse>;

public class RefreshSessionCommandHandler(
    ICurrentUserService currentUser,
    IHttpContextAccessor httpContextAccessor,
    IUserRepository userRepository,
    ITokenService tokenService) : IRequestHandler<RefreshSessionCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(RefreshSessionCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LoginResponse { Success = false, Message = "Unauthorized." };
        }

        var user = await userRepository.GetByIdAsync(currentUser.UserId.Value, cancellationToken);
        if (user is null || user.IsUnclaimedPlaceholder)
        {
            return new LoginResponse { Success = false, Message = "Unauthorized." };
        }

        if (!user.IsActive)
        {
            return new LoginResponse
            {
                Success = false,
                Message = "This account has been frozen pending a safety review."
            };
        }

        int? registeredDevicePk = null;
        var deviceClaim = httpContextAccessor.HttpContext?.User.FindFirstValue(SecurityStampHelper.DeviceIdClaimType);
        if (int.TryParse(deviceClaim, out var deviceId) && deviceId > 0)
        {
            registeredDevicePk = deviceId;
        }

        SecurityStampHelper.EnsureStamp(user);

        return new LoginResponse
        {
            Success = true,
            Message = "Session refreshed.",
            Token = tokenService.GenerateJwtToken(user, registeredDevicePk),
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email
            }
        };
    }
}
