using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Friends.Queries.GetHiddenUserIds;

public record GetHiddenUserIdsQuery : IRequest<HiddenUserIdsResponse>;

public class HiddenUserIdsResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<int> UserIds { get; set; } = Array.Empty<int>();
}

public class GetHiddenUserIdsQueryHandler(
    ICurrentUserService currentUser,
    IUserBlockRepository blockRepository) : IRequestHandler<GetHiddenUserIdsQuery, HiddenUserIdsResponse>
{
    public async Task<HiddenUserIdsResponse> Handle(GetHiddenUserIdsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new HiddenUserIdsResponse { Success = false, Message = "Unauthorized." };
        }

        var hidden = await blockRepository.GetHiddenUserIdsForViewerAsync(
            currentUser.UserId.Value,
            cancellationToken);

        return new HiddenUserIdsResponse
        {
            Success = true,
            Message = "Hidden users loaded.",
            UserIds = hidden.OrderBy(id => id).ToArray()
        };
    }
}
