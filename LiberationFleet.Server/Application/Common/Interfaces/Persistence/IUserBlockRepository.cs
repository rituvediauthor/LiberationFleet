using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IUserBlockRepository
{
    Task<bool> IsBlockedAsync(int blockerUserId, int blockedUserId, CancellationToken cancellationToken = default);
    Task<UserBlock?> GetBlockAsync(int blockerUserId, int blockedUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<int>> GetBlockedUserIdsAsync(int blockerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<int>> GetHiddenUserIdsForViewerAsync(int viewerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserBlock>> GetBlocksByBlockerWithUsersAsync(int blockerUserId, CancellationToken cancellationToken = default);
    Task AddAsync(UserBlock block, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(int blockerUserId, int blockedUserId, CancellationToken cancellationToken = default);
}
