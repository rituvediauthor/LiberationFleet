using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Friends.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Friends.Queries.GetBlockedUsers;

public record GetBlockedUsersQuery : IRequest<BlockedUserListResponse>;

public class GetBlockedUsersQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserBlockRepository blockRepository) : IRequestHandler<GetBlockedUsersQuery, BlockedUserListResponse>
{
    public async Task<BlockedUserListResponse> Handle(GetBlockedUsersQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new BlockedUserListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new BlockedUserListResponse { Success = false, Message = "You are not in a crew." };
        }

        var blocks = await blockRepository.GetBlocksByBlockerWithUsersAsync(userId, cancellationToken);
        var items = blocks
            .Where(b => b.Blocked is not null)
            .Select(b => new BlockedUserListItemDto
            {
                UserId = b.BlockedUserId,
                Username = b.Blocked!.Username,
                BlockedAt = b.CreatedAt
            })
            .OrderBy(i => i.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new BlockedUserListResponse
        {
            Success = true,
            Message = "Blocked users loaded.",
            Items = items
        };
    }
}
