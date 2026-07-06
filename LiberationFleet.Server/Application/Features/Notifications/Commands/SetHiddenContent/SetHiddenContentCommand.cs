using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Notifications.Commands.SetHiddenContent;

public record SetHiddenContentCommand(MutedContentType ContentType, int ResourceId, bool Hidden)
    : IRequest<NotificationOperationResponse>;

public class SetHiddenContentCommandHandler(
    ICurrentUserService currentUser,
    INotificationRepository notificationRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<SetHiddenContentCommand, NotificationOperationResponse>
{
    public async Task<NotificationOperationResponse> Handle(SetHiddenContentCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new NotificationOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (request.ResourceId <= 0)
        {
            return new NotificationOperationResponse { Success = false, Message = "Invalid resource." };
        }

        if (request.ContentType is not (MutedContentType.ChatRoom or MutedContentType.Forum))
        {
            return new NotificationOperationResponse { Success = false, Message = "This content cannot be hidden." };
        }

        var userId = currentUser.UserId.Value;

        if (request.Hidden)
        {
            var alreadyHidden = await notificationRepository.IsContentHiddenAsync(
                userId,
                request.ContentType,
                request.ResourceId,
                cancellationToken);

            if (!alreadyHidden)
            {
                await notificationRepository.AddHiddenContentAsync(new UserHiddenContent
                {
                    UserId = userId,
                    ContentType = request.ContentType,
                    ResourceId = request.ResourceId,
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken);
            }

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
            await notificationRepository.RemoveHiddenContentAsync(
                userId,
                request.ContentType,
                request.ResourceId,
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new NotificationOperationResponse
        {
            Success = true,
            Message = request.Hidden ? "Content hidden." : "Content unhidden."
        };
    }
}
