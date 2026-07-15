using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Security;

public static class SettingsLockHelper
{
    public static Task<(bool Allowed, string Message)> VerifySettingsPasswordAsync(
        User user,
        string? settingsPassword,
        IPasswordHasher passwordHasher)
    {
        if (!user.LockSettingsWithPassword)
        {
            return Task.FromResult((true, string.Empty));
        }

        if (string.IsNullOrWhiteSpace(user.SettingsLockPasswordHash))
        {
            return Task.FromResult((false, "Settings lock is enabled but no password is set. Re-enable settings lock with a password."));
        }

        if (string.IsNullOrWhiteSpace(settingsPassword))
        {
            return Task.FromResult((false, "Enter your settings password to save changes."));
        }

        if (!passwordHasher.Verify(settingsPassword, user.SettingsLockPasswordHash))
        {
            return Task.FromResult((false, "Incorrect settings password."));
        }

        return Task.FromResult((true, string.Empty));
    }

    public static async Task RecordSettingsChangedAlertAsync(
        int userId,
        string settingArea,
        ISecurityRepository securityRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        await securityRepository.AddAlertAsync(new SecurityAlert
        {
            UserId = userId,
            AlertType = Domain.Enums.SecurityAlertType.SettingsChanged,
            Title = "Settings changed",
            Message = $"{settingArea} preferences were updated.",
            OccurredAt = DateTime.UtcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
