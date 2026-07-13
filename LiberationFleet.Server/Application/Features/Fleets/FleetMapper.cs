using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Fleets;

public static class FleetMapper
{
    public static FleetDto MapFleet(Fleet fleet, int crewCount) => new()
    {
        Id = fleet.Id,
        Name = fleet.Name,
        CrewCount = crewCount,
        Privacy = fleet.Privacy.ToString(),
        Scope = fleet.Scope.ToString(),
        ZipCode = fleet.ZipCode,
        RadiusMiles = fleet.RadiusMiles,
        JoinCode = fleet.JoinCode,
        RequireApprovalForEdits = fleet.RequireApprovalForEdits,
        LibraryOfThingsEnabled = fleet.LibraryOfThingsEnabled,
        AllowCrewmateFileAttachments = fleet.AllowCrewmateFileAttachments,
        MinimumCrewmateTenureDaysForAttachments = fleet.MinimumCrewmateTenureDaysForAttachments,
        MinimumContributionForAttachments = fleet.MinimumContributionForAttachments,
        MinimumCrewmateTenureDaysForProposals = fleet.MinimumCrewmateTenureDaysForProposals,
        MinimumContributionForProposals = fleet.MinimumContributionForProposals
    };

    public static FleetRuleDto MapRule(FleetRule rule) => new()
    {
        Id = rule.Id,
        CreatedByUserId = rule.CreatedByUserId,
        CreatedByUsername = rule.CreatedByUser.Username,
        CreatedAt = rule.CreatedAt,
        UpdatedAt = rule.UpdatedAt,
        IsPublic = rule.IsPublic,
        Title = rule.Title ?? string.Empty,
        Description = rule.Description ?? string.Empty
    };
}
