using System.Text.Json.Serialization;

namespace LiberationFleet.Server.Application.Features.Crews;

/// <summary>
/// Season-facing aid accounting editable via aid-stat proposals (and stored as a pre-season draft).
/// </summary>
public sealed class AidSeasonAccountingDto
{
    public decimal CycleReceived { get; set; }
    public bool HasActiveCycle { get; set; }
    /// <summary>1-based reception order among locked/active cycles (1 = front of queue).</summary>
    public int? ReceptionOrder { get; set; }
    public bool AutoJoinSeasonOnStart { get; set; }
    public List<AidSurvivalThresholdDraftDto> SurvivalThresholds { get; set; } = [];
    public List<int> RemovedThresholdIds { get; set; } = [];

    [JsonIgnore]
    public decimal SurvivalReceivedTotal =>
        SurvivalThresholds.Sum(t => Math.Max(0m, t.ThresholdAmount - t.AmountRemaining));

    [JsonIgnore]
    public decimal TotalReceptionAmount => CycleReceived + SurvivalReceivedTotal;
}

public sealed class AidSurvivalThresholdDraftDto
{
    /// <summary>Existing <see cref="Domain.Entities.MonthlySurvivalThreshold"/> id, or null for a new row.</summary>
    public int? Id { get; set; }
    /// <summary>Original threshold need (for existing rows). New rows use AmountRemaining as ThresholdAmount.</summary>
    public decimal ThresholdAmount { get; set; }
    /// <summary>Money still due for this threshold.</summary>
    public decimal AmountRemaining { get; set; }
    /// <summary>1-based order (1 = first among survival thresholds).</summary>
    public int Order { get; set; }
}
