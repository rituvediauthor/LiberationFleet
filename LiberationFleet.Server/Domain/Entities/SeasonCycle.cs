namespace LiberationFleet.Server.Domain.Entities;

public class SeasonCycle
{
    public int Id { get; set; }
    public int CrewId { get; set; }
    public int UserId { get; set; }
    public DateTime SeasonStartDate { get; set; }
    public decimal CycleCapAtStart { get; set; }
    /// <summary>Effective cycle cap when this cycle last completed; used for catch-up when caps grow.</summary>
    public decimal CycleCapAtCompletion { get; set; }
    /// <summary>
    /// True for future-season primaries whose dollar cap is not frozen until that season starts.
    /// </summary>
    public bool CapIsProvisional { get; set; }
    /// <summary>
    /// Amount already carved from a provisional primary via emergency splits before the season locks.
    /// </summary>
    public decimal SplitReservedAmount { get; set; }
    public decimal TotalReceptionAmount { get; set; }
    public decimal SurvivalThresholdReceived { get; set; }
    public decimal CycleReceived { get; set; }
    public bool CycleCompleted { get; set; }
    public DateTime? CycleCompletedAt { get; set; }
    public decimal PriorityScoreAtSeasonStart { get; set; }
    public int ReceptionOrderPosition { get; set; }
    public bool HasCycleStarted { get; set; }
    public bool UsesSegmentCap { get; set; }
    public int? EmergencyRequestId { get; set; }
    public int? EmergencySplitOfferId { get; set; }

    public Crew Crew { get; set; } = null!;
    public User User { get; set; } = null!;
    public EmergencyRequest? EmergencyRequest { get; set; }
    public EmergencySplitOffer? EmergencySplitOffer { get; set; }
}
