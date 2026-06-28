using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class UserNotificationPreference
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public NotificationKind Kind { get; set; }
    public bool IsEnabled { get; set; } = true;

    public User User { get; set; } = null!;
}
