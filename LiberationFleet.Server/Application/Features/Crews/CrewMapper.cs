using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Crews;

public static class CrewMapper
{
    public static CrewDto MapCrew(Crew crew, int memberCount) => new()
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
        InNeedDefaultThreshold = crew.InNeedDefaultThreshold
    };
}
