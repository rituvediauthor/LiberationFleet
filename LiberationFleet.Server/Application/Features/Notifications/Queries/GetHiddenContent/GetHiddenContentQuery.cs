using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Notifications.Queries.GetHiddenContent;

public record GetHiddenContentQuery : IRequest<HiddenContentListResponse>;

public class GetHiddenContentQueryHandler(
    ICurrentUserService currentUser,
    INotificationRepository notificationRepository) : IRequestHandler<GetHiddenContentQuery, HiddenContentListResponse>
{
    public async Task<HiddenContentListResponse> Handle(GetHiddenContentQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new HiddenContentListResponse { Success = false, Message = "Unauthorized." };
        }

        var items = await notificationRepository.GetHiddenContentsAsync(currentUser.UserId.Value, cancellationToken);
        return new HiddenContentListResponse
        {
            Success = true,
            Message = "Hidden content loaded.",
            Items = items.Select(item => new HiddenContentDto
            {
                ContentType = item.ContentType,
                ResourceId = item.ResourceId
            }).ToList()
        };
    }
}
