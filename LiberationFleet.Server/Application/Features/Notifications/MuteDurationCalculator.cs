using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Notifications;

public static class MuteDurationCalculator
{
    public static DateTime? Until(MuteDuration? duration, DateTime utcNow) => duration switch
    {
        MuteDuration.OneHour => utcNow.AddHours(1),
        MuteDuration.EightHours => utcNow.AddHours(8),
        MuteDuration.OneDay => utcNow.AddDays(1),
        MuteDuration.OneWeek => utcNow.AddDays(7),
        _ => null
    };

    public static bool IsActive(DateTime? mutedUntil, DateTime utcNow) =>
        mutedUntil is null || mutedUntil > utcNow;
}
