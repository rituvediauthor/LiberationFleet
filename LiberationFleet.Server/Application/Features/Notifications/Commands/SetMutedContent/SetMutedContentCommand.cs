using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Notifications.Commands.SetMutedContent;

public record SetMutedContentCommand(MutedContentType ContentType, int ResourceId, bool Muted)
    : IRequest<NotificationOperationResponse>;

public class SetMutedContentCommandHandler(
    ICurrentUserService currentUser,
    INotificationRepository notificationRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<SetMutedContentCommand, NotificationOperationResponse>
{
    public async Task<NotificationOperationResponse> Handle(SetMutedContentCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new NotificationOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (request.ResourceId <= 0)
        {
            return new NotificationOperationResponse { Success = false, Message = "Invalid resource." };
        }

        var userId = currentUser.UserId.Value;

        if (request.Muted)
        {
            var alreadyMuted = await notificationRepository.IsContentMutedAsync(
                userId,
                request.ContentType,
                request.ResourceId,
                cancellationToken);

            if (!alreadyMuted)
            {
                await notificationRepository.AddMutedContentAsync(new UserMutedContent
                {
                    UserId = userId,
                    ContentType = request.ContentType,
                    ResourceId = request.ResourceId,
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken);
            }
        }
        else
        {
            await notificationRepository.RemoveMutedContentAsync(
                userId,
                request.ContentType,
                request.ResourceId,
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new NotificationOperationResponse
        {
            Success = true,
            Message = request.Muted ? "Notifications muted." : "Notifications unmuted."
        };
    }
}
