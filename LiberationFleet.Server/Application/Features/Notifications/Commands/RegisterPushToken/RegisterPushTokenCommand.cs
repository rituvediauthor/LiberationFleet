using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Notifications.Commands.RegisterPushToken;

public record RegisterPushTokenCommand(string Token, string Platform, string? DeviceId)
    : IRequest<NotificationOperationResponse>;

public class RegisterPushTokenCommandHandler(
    ICurrentUserService currentUser,
    IDevicePushTokenRepository pushTokens,
    IUnitOfWork unitOfWork) : IRequestHandler<RegisterPushTokenCommand, NotificationOperationResponse>
{
    public async Task<NotificationOperationResponse> Handle(
        RegisterPushTokenCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new NotificationOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 512)
        {
            return new NotificationOperationResponse { Success = false, Message = "Invalid push token." };
        }

        if (!PushPlatformParser.TryParse(request.Platform, out var platform))
        {
            return new NotificationOperationResponse
            {
                Success = false,
                Message = "Platform must be android or ios."
            };
        }

        var deviceId = string.IsNullOrWhiteSpace(request.DeviceId)
            ? null
            : request.DeviceId.Trim();
        if (deviceId is { Length: > 128 })
        {
            return new NotificationOperationResponse { Success = false, Message = "Invalid device id." };
        }

        await pushTokens.UpsertAsync(new DevicePushToken
        {
            UserId = currentUser.UserId.Value,
            Platform = platform,
            Token = request.Token.Trim(),
            DeviceId = deviceId
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new NotificationOperationResponse
        {
            Success = true,
            Message = "Push token registered."
        };
    }
}
