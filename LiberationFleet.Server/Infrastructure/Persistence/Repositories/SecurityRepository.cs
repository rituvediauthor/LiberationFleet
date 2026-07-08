using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class SecurityRepository(ApplicationDbContext context) : ISecurityRepository
{
    public Task<UserRegisteredDevice?> GetDeviceByDeviceIdAsync(int userId, string deviceId, CancellationToken cancellationToken = default) =>
        context.UserRegisteredDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId, cancellationToken);

    public Task<UserRegisteredDevice?> GetDeviceByIdAsync(int userId, int deviceId, CancellationToken cancellationToken = default) =>
        context.UserRegisteredDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Id == deviceId, cancellationToken);

    public async Task<IReadOnlyList<UserRegisteredDevice>> GetDevicesForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        await context.UserRegisteredDevices
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.LastSeenAt)
            .ToListAsync(cancellationToken);

    public async Task AddDeviceAsync(UserRegisteredDevice device, CancellationToken cancellationToken = default) =>
        await context.UserRegisteredDevices.AddAsync(device, cancellationToken);

    public Task RemoveDeviceAsync(UserRegisteredDevice device, CancellationToken cancellationToken = default)
    {
        context.UserRegisteredDevices.Remove(device);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<SecurityAlert>> GetAlertsForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        await context.SecurityAlerts
            .Include(a => a.RelatedDevice)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.OccurredAt)
            .ToListAsync(cancellationToken);

    public Task<SecurityAlert?> GetAlertByIdAsync(int userId, int alertId, CancellationToken cancellationToken = default) =>
        context.SecurityAlerts
            .Include(a => a.RelatedDevice)
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Id == alertId, cancellationToken);

    public async Task AddAlertAsync(SecurityAlert alert, CancellationToken cancellationToken = default) =>
        await context.SecurityAlerts.AddAsync(alert, cancellationToken);

    public Task<int> CountRecentAlertsAsync(int userId, SecurityAlertType alertType, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.Subtract(window);
        return context.SecurityAlerts
            .CountAsync(a => a.UserId == userId && a.AlertType == alertType && a.OccurredAt >= since, cancellationToken);
    }
}
