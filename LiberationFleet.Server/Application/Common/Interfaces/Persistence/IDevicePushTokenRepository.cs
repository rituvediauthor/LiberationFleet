using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IDevicePushTokenRepository
{
    Task UpsertAsync(DevicePushToken token, CancellationToken cancellationToken = default);

    Task RemoveByTokenAsync(int userId, string token, CancellationToken cancellationToken = default);

    Task RemoveAllForUserAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DevicePushToken>> GetActiveForUserAsync(int userId, CancellationToken cancellationToken = default);

    Task DisableAsync(int tokenId, CancellationToken cancellationToken = default);
}
