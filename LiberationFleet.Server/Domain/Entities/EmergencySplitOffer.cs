using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class EmergencySplitOffer
{
    public int Id { get; set; }
    public int EmergencyRequestId { get; set; }
    public int OffererUserId { get; set; }
    public decimal Amount { get; set; }
    public EmergencyOffererQueueRole OffererQueueRole { get; set; }
    public bool IsCancelled { get; set; }
    public int? RequesterEmergencyCycleId { get; set; }
    public int? OffererPaybackCycleId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public EmergencyRequest EmergencyRequest { get; set; } = null!;
    public User OffererUser { get; set; } = null!;
    public SeasonCycle? RequesterEmergencyCycle { get; set; }
    public SeasonCycle? OffererPaybackCycle { get; set; }
}
