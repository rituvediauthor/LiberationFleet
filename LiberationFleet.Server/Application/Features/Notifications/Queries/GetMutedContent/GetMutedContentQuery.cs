using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Notifications.Queries.GetMutedContent;

public record GetMutedContentQuery : IRequest<MutedContentListResponse>;

public class GetMutedContentQueryHandler(
    ICurrentUserService currentUser,
    INotificationRepository notificationRepository) : IRequestHandler<GetMutedContentQuery, MutedContentListResponse>
{
    public async Task<MutedContentListResponse> Handle(GetMutedContentQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new MutedContentListResponse { Success = false, Message = "Unauthorized." };
        }

        var items = await notificationRepository.GetMutedContentsAsync(currentUser.UserId.Value, cancellationToken);
        return new MutedContentListResponse
        {
            Success = true,
            Message = "Muted content loaded.",
            Items = items.Select(item => new MutedContentDto
            {
                ContentType = item.ContentType,
                ResourceId = item.ResourceId
            }).ToList()
        };
    }
}
