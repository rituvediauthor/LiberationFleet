using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IUserBlockRepository
{
    Task<bool> IsBlockedAsync(int blockerUserId, int blockedUserId, CancellationToken cancellationToken = default);
    Task<UserBlock?> GetBlockAsync(int blockerUserId, int blockedUserId, CancellationToken cancellationToken = default);
    Task AddAsync(UserBlock block, CancellationToken cancellationToken = default);
}
