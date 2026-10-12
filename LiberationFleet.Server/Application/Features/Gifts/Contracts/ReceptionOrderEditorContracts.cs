namespace LiberationFleet.Server.Application.Features.Gifts.Contracts;

public sealed class ReceptionOrderEditorResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool SeasonStarted { get; set; }
    public DateTime? SeasonStartDate { get; set; }
    public IReadOnlyList<ReceptionOrderEditorMemberDto> Members { get; set; } =
        Array.Empty<ReceptionOrderEditorMemberDto>();
    public IReadOnlyList<ReceptionOrderEditorThresholdDto> Thresholds { get; set; } =
        Array.Empty<ReceptionOrderEditorThresholdDto>();
    public IReadOnlyList<ReceptionOrderEditorCycleDto> Cycles { get; set; } =
        Array.Empty<ReceptionOrderEditorCycleDto>();
}

public sealed class ReceptionOrderEditorMemberDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool HasPrimaryCycle { get; set; }
}

public sealed class ReceptionOrderEditorThresholdDto
{
    public int? Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    /// <summary>Total need (threshold amount).</summary>
    public decimal AmountNeeded { get; set; }
    /// <summary>Still owed.</summary>
    public decimal AmountRemaining { get; set; }
    /// <summary>1-based crew-wide order among survival thresholds.</summary>
    public int ReceptionOrder { get; set; }
}

public sealed class ReceptionOrderEditorCycleDto
{
    public int? Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    /// <summary>Total cycle target (cap).</summary>
    public decimal Amount { get; set; }
    /// <summary>Still owed toward the cycle target.</summary>
    public decimal AmountRemaining { get; set; }
    /// <summary>True when this cycle is unlocked for reception (HasCycleStarted).</summary>
    public bool IsActive { get; set; }
    /// <summary>True when incomplete and not yet unlocked (!HasCycleStarted).</summary>
    public bool IsFrozen { get; set; }
    /// <summary>1-based reception order among season cycles.</summary>
    public int ReceptionOrder { get; set; }
    public bool CycleCompleted { get; set; }
    public bool IsEmergencyCycle { get; set; }
    public bool IsPaybackCycle { get; set; }
    public bool IsPrimary { get; set; }
    public bool CanRemove { get; set; }
}

public sealed class SaveReceptionOrderEditorRequest
{
    public List<ReceptionOrderEditorThresholdDto> Thresholds { get; set; } = [];
    public List<int> RemovedThresholdIds { get; set; } = [];
    public List<ReceptionOrderEditorCycleDto> Cycles { get; set; } = [];
    public List<int> RemovedCycleIds { get; set; } = [];
}

public sealed class SaveReceptionOrderEditorResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public ReceptionOrderEditorResponse? Editor { get; set; }
}
