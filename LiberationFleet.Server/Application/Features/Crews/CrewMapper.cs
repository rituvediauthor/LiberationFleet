using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Crews;

public static class CrewMapper
{
    public static CrewDto MapCrew(Crew crew, int memberCount, decimal monthlyGivingCapacity = 0m) => new()
    {
        Id = crew.Id,
        Name = crew.Name,
        MaxSize = crew.MaxSize,
        MemberCount = memberCount,
        Privacy = crew.Privacy.ToString(),
        Scope = crew.Scope.ToString(),
        ZipCode = crew.ZipCode,
        RadiusMiles = crew.RadiusMiles,
        JoinCode = crew.JoinCode,
        AllowSurvivalThresholds = crew.AllowSurvivalThresholds,
        RequireApprovalForEdits = crew.RequireApprovalForEdits,
        InNeedDefaultThreshold = crew.InNeedDefaultThreshold,
        LibraryOfThingsEnabled = crew.LibraryOfThingsEnabled,
        MemberCycleCapMode = crew.MemberCycleCapMode.ToString(),
        MemberCycleCapFixedAmount = crew.MemberCycleCapFixedAmount,
        MemberCycleCapMultiplier = crew.MemberCycleCapMultiplier,
        NonMemberCycleCapMode = crew.NonMemberCycleCapMode.ToString(),
        NonMemberCycleCapFixedAmount = crew.NonMemberCycleCapFixedAmount,
        NonMemberCycleCapMultiplier = crew.NonMemberCycleCapMultiplier,
        AllowCrewmateFileAttachments = crew.AllowCrewmateFileAttachments,
        MinimumCrewmateTenureDaysForAttachments = crew.MinimumCrewmateTenureDaysForAttachments,
        MinimumContributionForAttachments = crew.MinimumContributionForAttachments,
        MinimumCrewmateTenureDaysForProposals = crew.MinimumCrewmateTenureDaysForProposals,
        MinimumContributionForProposals = crew.MinimumContributionForProposals,
        AllowCrossCrewGiving = crew.AllowCrossCrewGiving,
        MonthlyGivingCapacity = monthlyGivingCapacity
    };
}
