using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common;

public static class ContentTenureCalculator
{
    public static int GetTenureDays(long accruedTicks, DateTime? clockStartedAtUtc, DateTime utcNow)
    {
        var total = TimeSpan.FromTicks(Math.Max(0, accruedTicks));
        if (clockStartedAtUtc.HasValue && clockStartedAtUtc.Value < utcNow)
        {
            total += utcNow - clockStartedAtUtc.Value;
        }

        return Math.Max(0, (int)Math.Floor(total.TotalDays));
    }

    public static int GetTenureDays(UserCrewContentTenure? tenure, DateTime utcNow) =>
        tenure is null
            ? 0
            : GetTenureDays(tenure.AccruedTicks, tenure.ClockStartedAtUtc, utcNow);

    public static int GetTenureDays(UserFleetContentTenure? tenure, DateTime utcNow) =>
        tenure is null
            ? 0
            : GetTenureDays(tenure.AccruedTicks, tenure.ClockStartedAtUtc, utcNow);

    public static void Resume(ref long accruedTicks, ref DateTime? clockStartedAtUtc, DateTime utcNow)
    {
        if (clockStartedAtUtc.HasValue)
        {
            return;
        }

        clockStartedAtUtc = utcNow;
    }

    public static void Pause(ref long accruedTicks, ref DateTime? clockStartedAtUtc, DateTime utcNow)
    {
        if (!clockStartedAtUtc.HasValue)
        {
            return;
        }

        if (clockStartedAtUtc.Value < utcNow)
        {
            accruedTicks += (utcNow - clockStartedAtUtc.Value).Ticks;
        }

        clockStartedAtUtc = null;
    }
}
