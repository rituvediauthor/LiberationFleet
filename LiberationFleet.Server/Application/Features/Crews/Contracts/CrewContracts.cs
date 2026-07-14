namespace LiberationFleet.Server.Application.Features.Crews.Contracts;

public class CrewDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MaxSize { get; set; }
    public int MemberCount { get; set; }
    public string Privacy { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string? ZipCode { get; set; }
    public int? RadiusMiles { get; set; }
    public string JoinCode { get; set; } = string.Empty;
    public double? DistanceMiles { get; set; }
    public bool AllowSurvivalThresholds { get; set; } = true;
    public bool RequireApprovalForEdits { get; set; } = true;
    public decimal InNeedDefaultThreshold { get; set; } = 20m;
    public bool LibraryOfThingsEnabled { get; set; } = true;
    public string MemberCycleCapMode { get; set; } = "CapacityBased";
    public decimal MemberCycleCapFixedAmount { get; set; }
    public decimal MemberCycleCapMultiplier { get; set; } = 2m;
    public string NonMemberCycleCapMode { get; set; } = "CapacityBased";
    public decimal NonMemberCycleCapFixedAmount { get; set; }
    public decimal NonMemberCycleCapMultiplier { get; set; } = 0.5m;
    public bool AllowCrewmateFileAttachments { get; set; }
    public int MinimumCrewmateTenureDaysForAttachments { get; set; }
    public decimal MinimumContributionForAttachments { get; set; }
    public int MinimumCrewmateTenureDaysForProposals { get; set; }
    public decimal MinimumContributionForProposals { get; set; }
    public bool AllowCrossCrewGiving { get; set; }
    public decimal MonthlyGivingCapacity { get; set; }
}

public class UpdateCrewRequest
{
    public string Name { get; set; } = string.Empty;
    public int MaxSize { get; set; }
    public string Privacy { get; set; } = "Public";
    public string Scope { get; set; } = "Online";
    public string? ZipCode { get; set; }
    public int? RadiusMiles { get; set; }
    public bool AllowSurvivalThresholds { get; set; } = true;
    public bool RequireApprovalForEdits { get; set; } = true;
    public decimal InNeedDefaultThreshold { get; set; } = 20m;
    public bool LibraryOfThingsEnabled { get; set; } = true;
    public string MemberCycleCapMode { get; set; } = "CapacityBased";
    public decimal MemberCycleCapFixedAmount { get; set; }
    public decimal MemberCycleCapMultiplier { get; set; } = 2m;
    public string NonMemberCycleCapMode { get; set; } = "CapacityBased";
    public decimal NonMemberCycleCapFixedAmount { get; set; }
    public decimal NonMemberCycleCapMultiplier { get; set; } = 0.5m;
    public bool AllowCrewmateFileAttachments { get; set; }
    public int MinimumCrewmateTenureDaysForAttachments { get; set; }
    public decimal MinimumContributionForAttachments { get; set; }
    public int MinimumCrewmateTenureDaysForProposals { get; set; }
    public decimal MinimumContributionForProposals { get; set; }
    public bool AllowCrossCrewGiving { get; set; }
}

public class CrewMembershipStatusDto
{
    public bool HasCrew { get; set; }
    public int? CrewId { get; set; }
    public string? CrewName { get; set; }
    public string? JoinCode { get; set; }
    public bool LibraryOfThingsEnabled { get; set; } = true;
    public bool CanAttachFilesToCrewContent { get; set; }
    public bool CanCreateProposals { get; set; }
    public bool CanCreateFleetProposals { get; set; }
    public bool CanAttachFilesToFleetContent { get; set; }
    public bool CanExportCrewData { get; set; }
}

public class CrewOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public CrewDto? Crew { get; set; }
    public bool ProposalsSubmitted { get; set; }
    public int ProposalsCreated { get; set; }
}

public class CrewSearchResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<CrewDto> Items { get; set; } = Array.Empty<CrewDto>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
