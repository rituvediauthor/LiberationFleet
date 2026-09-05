namespace LiberationFleet.Server.Domain.Entities;

public class UserPaymentPlatform
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    /// <summary>
    /// Bound crew catalog row while the user is in that crew. Null after leave so the handle
    /// can travel with the user and be remounted onto the next crew by <see cref="PlatformName"/>.
    /// </summary>
    public int? CrewPaymentPlatformId { get; set; }
    public CrewPaymentPlatform? CrewPaymentPlatform { get; set; }
    /// <summary>Platform display name preserved across crew changes.</summary>
    public string PlatformName { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public bool IsPreferred { get; set; }
}
