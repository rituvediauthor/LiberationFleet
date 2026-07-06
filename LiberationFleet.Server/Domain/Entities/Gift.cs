using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class Gift
{
    public int Id { get; set; }
    public int CrewId { get; set; }
    public int GiverUserId { get; set; }
    public int RecipientUserId { get; set; }
    public int? MiddlemanUserId { get; set; }
    public GiftType Type { get; set; }
    public decimal Amount { get; set; }
    public int CrewPaymentPlatformId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? InitiatedGiftId { get; set; }
    public bool IsSurvivalThreshold { get; set; }
    public bool CountsTowardReception { get; set; } = true;
    public bool IsCustomGift { get; set; }
    public bool CountsTowardContribution { get; set; } = true;
    public bool ReceptionApplied { get; set; }
    public GiftVerificationStatus VerificationStatus { get; set; } = GiftVerificationStatus.Pending;
    public int? EmergencyRequestId { get; set; }
    public int? SeasonCycleId { get; set; }

    public Crew Crew { get; set; } = null!;
    public User GiverUser { get; set; } = null!;
    public User RecipientUser { get; set; } = null!;
    public User? MiddlemanUser { get; set; }
    public Gift? InitiatedGift { get; set; }
    public CrewPaymentPlatform CrewPaymentPlatform { get; set; } = null!;
}
