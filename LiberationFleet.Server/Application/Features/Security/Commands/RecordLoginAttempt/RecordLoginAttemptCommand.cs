using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Security.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Security.Commands.RecordLoginAttempt;

public record RecordLoginAttemptCommand(
    int? UserId,
    string UsernameOrEmail,
    bool Success,
    string? DeviceId,
    string? DeviceName,
    string? UserAgent) : IRequest;

public class RecordLoginAttemptCommandHandler(
    IUserRepository userRepository,
    ISecurityRepository securityRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<RecordLoginAttemptCommand>
{
    private const int SuspiciousFailureThreshold = 5;
    private static readonly TimeSpan SuspiciousWindow = TimeSpan.FromMinutes(15);

    public async Task Handle(RecordLoginAttemptCommand request, CancellationToken cancellationToken)
    {
        var user = request.UserId.HasValue
            ? await userRepository.GetByIdWithProfileAsync(request.UserId.Value, cancellationToken)
            : await userRepository.GetByEmailOrUsernameAsync(request.UsernameOrEmail, cancellationToken);

        if (user is null)
        {
            return;
        }

        if (!request.Success)
        {
            user.FailedLoginAttempts += 1;
            user.LastFailedLoginAt = DateTime.UtcNow;
            await userRepository.UpdateAsync(user, cancellationToken);

            if (user.FailedLoginAttempts >= SuspiciousFailureThreshold)
            {
                var recent = await securityRepository.CountRecentAlertsAsync(
                    user.Id,
                    SecurityAlertType.SuspiciousPasswordFailures,
                    SuspiciousWindow,
                    cancellationToken);

                if (recent == 0)
                {
                    await securityRepository.AddAlertAsync(new SecurityAlert
                    {
                        UserId = user.Id,
                        AlertType = SecurityAlertType.SuspiciousPasswordFailures,
                        Title = "Suspicious password attempts",
                        Message = $"There were {user.FailedLoginAttempts} failed sign-in attempts recently.",
                        OccurredAt = DateTime.UtcNow
                    }, cancellationToken);
                }
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        user.FailedLoginAttempts = 0;
        user.LastFailedLoginAt = null;
        await userRepository.UpdateAsync(user, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var deviceId = request.DeviceId.Trim();
        var existing = await securityRepository.GetDeviceByDeviceIdAsync(user.Id, deviceId, cancellationToken);
        if (existing is not null)
        {
            if (existing.IsBlocked)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }

            existing.LastSeenAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(request.UserAgent))
            {
                existing.UserAgent = request.UserAgent.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.DeviceName))
            {
                existing.DisplayName = request.DeviceName.Trim();
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var device = new UserRegisteredDevice
        {
            UserId = user.Id,
            DeviceId = deviceId,
            DisplayName = string.IsNullOrWhiteSpace(request.DeviceName) ? "Unknown device" : request.DeviceName.Trim(),
            UserAgent = request.UserAgent?.Trim() ?? string.Empty,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            IsTrusted = false
        };

        await securityRepository.AddDeviceAsync(device, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await securityRepository.AddAlertAsync(new SecurityAlert
        {
            UserId = user.Id,
            AlertType = SecurityAlertType.StrangeDeviceSignIn,
            Title = "Strange device signed in",
            Message = $"A sign-in was detected from {device.DisplayName} on {device.LastSeenAt:MMM d, yyyy}.",
            OccurredAt = DateTime.UtcNow,
            RelatedDeviceId = device.Id
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
