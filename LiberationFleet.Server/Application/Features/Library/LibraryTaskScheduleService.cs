using System.Globalization;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library;

public static class LibraryTaskScheduleService
{
    public const int MaxGeneratedInstances = 15;

    public static IReadOnlyList<DateTime> GenerateUpcomingInstances(
        LibraryTask task,
        DateTime utcNow,
        int maxCount = MaxGeneratedInstances)
    {
        if (maxCount <= 0)
        {
            return Array.Empty<DateTime>();
        }

        if (!task.HasDeadline)
        {
            // Completions create their own awaiting instances; no open slot required.
            return Array.Empty<DateTime>();
        }

        if (!task.IsRecurring || task.Frequency == LibraryTaskRecurrenceFrequency.None)
        {
            if (task.OneShotDueAt.HasValue && task.OneShotDueAt.Value >= utcNow.AddMinutes(-1))
            {
                return [NormalizeTime(task, task.OneShotDueAt.Value)];
            }

            return Array.Empty<DateTime>();
        }

        return task.Frequency switch
        {
            LibraryTaskRecurrenceFrequency.Daily => GenerateDaily(task, utcNow, maxCount),
            LibraryTaskRecurrenceFrequency.Weekly => GenerateWeekly(task, utcNow, maxCount),
            LibraryTaskRecurrenceFrequency.Monthly => GenerateMonthly(task, utcNow, maxCount),
            LibraryTaskRecurrenceFrequency.Yearly => GenerateYearly(task, utcNow, maxCount),
            _ => Array.Empty<DateTime>()
        };
    }

    public static string BuildScheduleSummary(LibraryTask task)
    {
        if (!task.HasDeadline)
        {
            return "When you can";
        }

        if (!task.IsRecurring || task.Frequency == LibraryTaskRecurrenceFrequency.None)
        {
            if (!task.OneShotDueAt.HasValue)
            {
                return "One-time";
            }

            return task.TimeSpecific
                ? task.OneShotDueAt.Value.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture)
                : task.OneShotDueAt.Value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
        }

        var interval = Math.Max(1, task.Interval);
        var spaced = task.IsSpaced && interval > 1;
        var timeSuffix = task.TimeSpecific && task.SpecificTimeMinutes.HasValue
            ? $" at {FormatMinutes(task.SpecificTimeMinutes.Value)}"
            : string.Empty;

        return task.Frequency switch
        {
            LibraryTaskRecurrenceFrequency.Daily => spaced
                ? $"Every {interval} days{timeSuffix}"
                : $"Daily{timeSuffix}",
            LibraryTaskRecurrenceFrequency.Weekly => BuildWeeklySummary(task, spaced, interval, timeSuffix),
            LibraryTaskRecurrenceFrequency.Monthly => BuildMonthlySummary(task, spaced, interval, timeSuffix),
            LibraryTaskRecurrenceFrequency.Yearly => BuildYearlySummary(task, spaced, interval, timeSuffix),
            _ => "Recurring"
        };
    }

    private static string BuildWeeklySummary(LibraryTask task, bool spaced, int interval, string timeSuffix)
    {
        var days = ParseInts(task.WeekDays);
        var dayNames = days
            .Select(d => CultureInfo.InvariantCulture.DateTimeFormat.GetDayName((DayOfWeek)(d % 7)))
            .ToList();
        var dayPart = dayNames.Count > 0 ? $" on {string.Join(", ", dayNames)}" : string.Empty;
        if (spaced)
        {
            return interval == 2
                ? $"Every other week{dayPart}{timeSuffix}"
                : $"Every {interval} weeks{dayPart}{timeSuffix}";
        }

        return $"Weekly{dayPart}{timeSuffix}";
    }

    private static string BuildMonthlySummary(LibraryTask task, bool spaced, int interval, string timeSuffix)
    {
        var days = ParseInts(task.MonthDays);
        var dayPart = days.Count > 0
            ? $" on day{(days.Count == 1 ? string.Empty : "s")} {string.Join(", ", days.Select(FormatMonthDay))}"
            : string.Empty;
        if (spaced)
        {
            return $"Every {interval} months{dayPart}{timeSuffix}";
        }

        return $"Monthly{dayPart}{timeSuffix}";
    }

    private static string BuildYearlySummary(LibraryTask task, bool spaced, int interval, string timeSuffix)
    {
        var month = task.YearMonth is >= 1 and <= 12
            ? CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(task.YearMonth.Value)
            : null;
        var day = task.YearDay is >= 1 and <= 31 ? FormatMonthDay(task.YearDay.Value) : null;
        var datePart = month is not null && day is not null
            ? $" on {month} {day}"
            : month is not null
                ? $" in {month}"
                : string.Empty;
        if (spaced)
        {
            return $"Every {interval} years{datePart}{timeSuffix}";
        }

        return $"Yearly{datePart}{timeSuffix}";
    }

    private static string FormatMonthDay(int day) =>
        day >= 31 ? "31 (or last day of month)" : day.ToString(CultureInfo.InvariantCulture);

    private static IReadOnlyList<DateTime> GenerateDaily(LibraryTask task, DateTime utcNow, int maxCount)
    {
        var interval = Math.Max(1, task.IsSpaced ? task.Interval : 1);
        var start = utcNow.Date;
        var results = new List<DateTime>(maxCount);
        for (var i = 0; results.Count < maxCount && i < maxCount * interval + 14; i++)
        {
            var day = start.AddDays(i);
            if (interval > 1)
            {
                var daysSinceEpoch = (int)(day - DateTime.UnixEpoch.Date).TotalDays;
                if (daysSinceEpoch % interval != 0)
                {
                    continue;
                }
            }

            var at = ApplyTime(task, day);
            if (at >= utcNow)
            {
                results.Add(at);
            }
        }

        return results;
    }

    private static IReadOnlyList<DateTime> GenerateWeekly(LibraryTask task, DateTime utcNow, int maxCount)
    {
        var interval = Math.Max(1, task.IsSpaced ? task.Interval : 1);
        var weekDays = ParseInts(task.WeekDays);
        if (weekDays.Count == 0 || !task.DaySpecific)
        {
            weekDays = [(int)utcNow.DayOfWeek];
        }

        var results = new List<DateTime>(maxCount);
        var start = utcNow.Date;
        var anchorWeekStart = start.AddDays(-(int)start.DayOfWeek);

        for (var weekOffset = 0; results.Count < maxCount && weekOffset < maxCount * interval + 52; weekOffset++)
        {
            if (interval > 1)
            {
                var weeksSinceAnchor = weekOffset;
                if (weeksSinceAnchor % interval != 0)
                {
                    continue;
                }
            }

            var weekStart = anchorWeekStart.AddDays(weekOffset * 7);
            foreach (var dow in weekDays.OrderBy(d => d))
            {
                var day = weekStart.AddDays(dow);
                var at = ApplyTime(task, day);
                if (at >= utcNow)
                {
                    results.Add(at);
                    if (results.Count >= maxCount)
                    {
                        break;
                    }
                }
            }
        }

        return results.OrderBy(d => d).Take(maxCount).ToList();
    }

    private static IReadOnlyList<DateTime> GenerateMonthly(LibraryTask task, DateTime utcNow, int maxCount)
    {
        var interval = Math.Max(1, task.IsSpaced ? task.Interval : 1);
        var monthDays = ParseInts(task.MonthDays);
        if (monthDays.Count == 0 || !task.DaySpecific)
        {
            monthDays = [Math.Min(utcNow.Day, 28)];
        }

        var results = new List<DateTime>(maxCount);
        var cursor = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var monthOffset = 0; results.Count < maxCount && monthOffset < maxCount * interval + 36; monthOffset++)
        {
            if (interval > 1 && monthOffset % interval != 0)
            {
                continue;
            }

            var monthStart = cursor.AddMonths(monthOffset);
            var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
            foreach (var dayNum in monthDays.OrderBy(d => d))
            {
                var day = dayNum >= 31 ? daysInMonth : Math.Min(dayNum, daysInMonth);
                var at = ApplyTime(task, new DateTime(monthStart.Year, monthStart.Month, day, 0, 0, 0, DateTimeKind.Utc));
                if (at >= utcNow)
                {
                    results.Add(at);
                    if (results.Count >= maxCount)
                    {
                        break;
                    }
                }
            }
        }

        return results.OrderBy(d => d).Take(maxCount).ToList();
    }

    private static IReadOnlyList<DateTime> GenerateYearly(LibraryTask task, DateTime utcNow, int maxCount)
    {
        var interval = Math.Max(1, task.IsSpaced ? task.Interval : 1);
        var month = task.YearMonth is >= 1 and <= 12 ? task.YearMonth.Value : utcNow.Month;
        var dayPref = task.YearDay is >= 1 and <= 31 ? task.YearDay.Value : utcNow.Day;

        var results = new List<DateTime>(maxCount);
        for (var yearOffset = 0; results.Count < maxCount && yearOffset < maxCount * interval + 20; yearOffset++)
        {
            if (interval > 1 && yearOffset % interval != 0)
            {
                continue;
            }

            var year = utcNow.Year + yearOffset;
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var day = dayPref >= 31 ? daysInMonth : Math.Min(dayPref, daysInMonth);
            var at = ApplyTime(task, new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc));
            if (at >= utcNow)
            {
                results.Add(at);
            }
        }

        return results;
    }

    private static DateTime ApplyTime(LibraryTask task, DateTime day)
    {
        if (!task.TimeSpecific || !task.SpecificTimeMinutes.HasValue)
        {
            return DateTime.SpecifyKind(day.Date, DateTimeKind.Utc);
        }

        var minutes = Math.Clamp(task.SpecificTimeMinutes.Value, 0, 23 * 60 + 59);
        return DateTime.SpecifyKind(day.Date.AddMinutes(minutes), DateTimeKind.Utc);
    }

    private static DateTime NormalizeTime(LibraryTask task, DateTime dueAt) =>
        task.TimeSpecific ? dueAt : DateTime.SpecifyKind(dueAt.Date, DateTimeKind.Utc);

    private static string FormatMinutes(int minutes)
    {
        var clamped = Math.Clamp(minutes, 0, 23 * 60 + 59);
        var tod = TimeSpan.FromMinutes(clamped);
        var dt = DateTime.Today.Add(tod);
        return dt.ToString("h:mm tt", CultureInfo.InvariantCulture);
    }

    public static IReadOnlyList<int> ParseInts(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return Array.Empty<int>();
        }

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var n) ? n : (int?)null)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .Distinct()
            .OrderBy(n => n)
            .ToList();
    }

    public static string? JoinInts(IEnumerable<int>? values)
    {
        if (values is null)
        {
            return null;
        }

        var list = values.Distinct().OrderBy(v => v).ToList();
        return list.Count == 0 ? null : string.Join(',', list);
    }
}
