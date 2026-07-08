using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class SecurityAlert
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public SecurityAlertType AlertType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
    public int? RelatedDeviceId { get; set; }

    public User User { get; set; } = null!;
    public UserRegisteredDevice? RelatedDevice { get; set; }
}
