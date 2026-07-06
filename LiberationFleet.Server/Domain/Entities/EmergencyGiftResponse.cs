namespace LiberationFleet.Server.Domain.Entities;

public class EmergencyGiftResponse
{
    public int Id { get; set; }
    public int EmergencyRequestId { get; set; }
    public int GiverUserId { get; set; }
    public int? GiftId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public EmergencyRequest EmergencyRequest { get; set; } = null!;
    public User GiverUser { get; set; } = null!;
    public Gift? Gift { get; set; }
}
