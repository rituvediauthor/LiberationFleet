using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IFriendshipRepository
{
    Task<Friendship?> GetBetweenUsersAsync(int userId, int otherUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Friendship>> GetForUserAsync(int userId, CancellationToken cancellationToken = default);
    Task AddAsync(Friendship friendship, CancellationToken cancellationToken = default);
    void Remove(Friendship friendship);
}
