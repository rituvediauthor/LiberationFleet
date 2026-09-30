using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Auth.Contracts;
using LiberationFleet.Server.Application.Features.Security;
using LiberationFleet.Server.Application.Features.Security.Commands.RecordLoginAttempt;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Auth.Commands.VerifyMfaLogin;

public record VerifyMfaLoginCommand(VerifyMfaLoginRequest Request) : IRequest<LoginResponse>;

public class VerifyMfaLoginCommandHandler(
    IEmailMfaService emailMfaService,
    IUserRepository userRepository,
    ISecurityRepository securityRepository,
    IUnitOfWork unitOfWork,
    ITokenService tokenService,
    IMediator mediator,
    ILogger<VerifyMfaLoginCommandHandler> logger) : IRequestHandler<VerifyMfaLoginCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(VerifyMfaLoginCommand request, CancellationToken cancellationToken)
    {
        var body = request.Request;
        var verified = await emailMfaService.VerifyAsync(body.MfaChallengeToken, body.Code, cancellationToken);
        if (!verified.Success || verified.Challenge is null)
        {
            return new LoginResponse { Success = false, Message = verified.Message };
        }

        var challenge = verified.Challenge;
        if (challenge.Purpose != EmailMfaPurpose.Login)
        {
            return new LoginResponse { Success = false, Message = "Invalid verification session." };
        }

        var user = challenge.User;
        if (!user.IsActive || !user.TwoFactorEnabled)
        {
            return new LoginResponse { Success = false, Message = "This account cannot complete MFA sign-in." };
        }

        if (!string.IsNullOrWhiteSpace(challenge.DeviceId))
        {
            var device = await securityRepository.GetDeviceByDeviceIdAsync(user.Id, challenge.DeviceId, cancellationToken);
            if (device?.IsBlocked == true)
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = "This device has been blocked from signing in."
                };
            }
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.FailedLoginAttempts = 0;
        user.LastFailedLoginAt = null;
        await userRepository.UpdateAsync(user, cancellationToken);

        await mediator.Send(new RecordLoginAttemptCommand(
            user.Id,
            user.Email,
            Success: true,
            challenge.DeviceId,
            challenge.DeviceName,
            challenge.UserAgent), cancellationToken);

        int? registeredDevicePk = null;
        if (!string.IsNullOrWhiteSpace(challenge.DeviceId))
        {
            var device = await securityRepository.GetDeviceByDeviceIdAsync(user.Id, challenge.DeviceId, cancellationToken);
            registeredDevicePk = device?.Id;
        }

        SecurityStampHelper.EnsureStamp(user);
        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User completed MFA login: {Email}", user.Email);

        return new LoginResponse
        {
            Success = true,
            Message = "Login successful",
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
