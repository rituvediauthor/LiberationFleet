using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Security.Contracts;

public class SecuritySettingsDto
{
    public bool TwoFactorEnabled { get; set; }
    public bool LockSettingsWithPassword { get; set; }
    public bool HasSettingsLockPassword { get; set; }
}

public class SecuritySettingsResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public SecuritySettingsDto? Settings { get; set; }
}

public class UpdateSecuritySettingsRequest
{
    public bool? TwoFactorEnabled { get; set; }
    public bool? LockSettingsWithPassword { get; set; }
    public string? NewSettingsLockPassword { get; set; }
    public string? CurrentSettingsLockPassword { get; set; }
    public string? SettingsPassword { get; set; }
}

public class RegisteredDeviceDto
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsTrusted { get; set; }
    public bool IsBlocked { get; set; }
    public bool IsCurrent { get; set; }
}

public class RegisteredDevicesResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<RegisteredDeviceDto> Devices { get; set; } = Array.Empty<RegisteredDeviceDto>();
}

public class SecurityAlertDto
{
    public int Id { get; set; }
    public SecurityAlertType AlertType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public bool IsRead { get; set; }
    public int? RelatedDeviceId { get; set; }
    public string? RelatedDeviceName { get; set; }
    public bool CanManageDevice { get; set; }
}

public class SecurityAlertsResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<SecurityAlertDto> Alerts { get; set; } = Array.Empty<SecurityAlertDto>();
}

public class SecurityOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class VerifySettingsPasswordRequest
{
    public string SettingsPassword { get; set; } = string.Empty;
}

public class VerifySettingsPasswordResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
