using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Proposals;

/// <summary>
/// Crew/fleet-scoped proposal auto-resolve timer configuration.
/// </summary>
public sealed record ProposalAutoResolveSettings(
    bool AutoResolveOverTime = true,
    int BaseAutoResolveHours = 24,
    bool ChangeAutoResolveTimerOnFirstReject = true,
    int AutoResolveHoursAfterFirstReject = 168)
{
    public const int MinHours = 1;
    public const int MaxHours = 8760;

    public static ProposalAutoResolveSettings Defaults { get; } = new();

    public static ProposalAutoResolveSettings From(Crew crew) => new(
        crew.AutoResolveOverTime,
        crew.BaseAutoResolveHours,
        crew.ChangeAutoResolveTimerOnFirstReject,
        crew.AutoResolveHoursAfterFirstReject);

    public static ProposalAutoResolveSettings From(Fleet fleet) => new(
        fleet.AutoResolveOverTime,
        fleet.BaseAutoResolveHours,
        fleet.ChangeAutoResolveTimerOnFirstReject,
        fleet.AutoResolveHoursAfterFirstReject);

    public DateTime? ComputeTimerEnd(DateTime utcNow, int disapproveCount)
    {
        if (!AutoResolveOverTime)
        {
            return null;
        }

        if (disapproveCount > 0
            && ChangeAutoResolveTimerOnFirstReject)
        {
            return utcNow.AddHours(AutoResolveHoursAfterFirstReject);
        }

        return utcNow.AddHours(BaseAutoResolveHours);
    }
}
