using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Notifications.Commands.UnregisterPushToken;

public record UnregisterPushTokenCommand(string? Token, bool AllDevices)
    : IRequest<NotificationOperationResponse>;

public class UnregisterPushTokenCommandHandler(
    ICurrentUserService currentUser,
    IDevicePushTokenRepository pushTokens,
    IUnitOfWork unitOfWork) : IRequestHandler<UnregisterPushTokenCommand, NotificationOperationResponse>
{
    public async Task<NotificationOperationResponse> Handle(
        UnregisterPushTokenCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new NotificationOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;

        if (request.AllDevices)
        {
            await pushTokens.RemoveAllForUserAsync(userId, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.Token))
        {
            await pushTokens.RemoveByTokenAsync(userId, request.Token.Trim(), cancellationToken);
        }
        else
        {
            return new NotificationOperationResponse
            {
                Success = false,
                Message = "Provide a token or set allDevices."
            };
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new NotificationOperationResponse
        {
            Success = true,
            Message = "Push token removed."
        };
    }
}
