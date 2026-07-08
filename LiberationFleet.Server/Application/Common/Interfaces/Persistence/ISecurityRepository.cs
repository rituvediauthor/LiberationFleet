using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface ISecurityRepository
{
    Task<UserRegisteredDevice?> GetDeviceByDeviceIdAsync(int userId, string deviceId, CancellationToken cancellationToken = default);
    Task<UserRegisteredDevice?> GetDeviceByIdAsync(int userId, int deviceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserRegisteredDevice>> GetDevicesForUserAsync(int userId, CancellationToken cancellationToken = default);
    Task AddDeviceAsync(UserRegisteredDevice device, CancellationToken cancellationToken = default);
    Task RemoveDeviceAsync(UserRegisteredDevice device, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SecurityAlert>> GetAlertsForUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<SecurityAlert?> GetAlertByIdAsync(int userId, int alertId, CancellationToken cancellationToken = default);
    Task AddAlertAsync(SecurityAlert alert, CancellationToken cancellationToken = default);
    Task<int> CountRecentAlertsAsync(int userId, SecurityAlertType alertType, TimeSpan window, CancellationToken cancellationToken = default);
}
