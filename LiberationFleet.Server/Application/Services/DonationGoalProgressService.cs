using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Services;

public class DonationGoalProgressService(
    IDonationGoalRepository goalRepository,
    IUnitOfWork unitOfWork) : IDonationGoalProgressService
{
    private static readonly TimeZoneInfo Eastern = ResolveEasternTimeZone();

    public async Task<DonationGoalCurrentDto> GetCurrentAsync(
        DateTime? utcNow = null,
        CancellationToken cancellationToken = default)
    {
        var now = NormalizeUtc(utcNow);
        var goals = await goalRepository.GetAllOrderedAsync(cancellationToken);
        if (EnsureCalendarTransitions(goals, now))
        {
            // Persist Nov cycle markers even when nobody donates that day.
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var active = ResolveActive(goals);
        var next = ResolveNext(goals, active);
        return ToDto(active, next);
    }

    public async Task ApplyDonationAsync(
        long amountCents,
        DateTime? utcNow = null,
        CancellationToken cancellationToken = default)
    {
        if (amountCents <= 0)
        {
            return;
        }

        var now = NormalizeUtc(utcNow);
        var goals = await goalRepository.GetAllOrderedAsync(cancellationToken);
        EnsureCalendarTransitions(goals, now);

        var remaining = amountCents;
        // Guard against pathological loops if catalog is misconfigured.
        for (var i = 0; i < 32 && remaining > 0; i++)
        {
            var active = ResolveActive(goals);
            if (active is null)
            {
                break;
            }

            var room = active.TargetCents - active.ProgressCents;
            if (room < 0)
            {
                room = 0;
            }

            if (remaining < room)
            {
                active.ProgressCents += remaining;
                active.UpdatedAt = now;
                remaining = 0;
                break;
            }

            remaining -= room;
            CompleteGoal(active, now);

            if (active.Key == DonationGoalKeys.LiveSixMonths)
            {
                // One-time only: credit into the first Live-for-one-year instance that follows.
                // Later November restarts do not get this carryover again.
                SeedLiveOneYearFromSixMonths(goals, now);
            }

            if (active.Recurrence == DonationGoalRecurrence.Repeating)
            {
                // Restart with leftover; same goal stays active.
                active.ProgressCents = 0;
                active.IsCompleted = false;
                active.CompletedAt = null;
                active.CompletionsThisYear += 1;
                active.UpdatedAt = now;
            }
        }

        // One-times may have just finished — stamp the current Nov cycle without wiping progress.
        EnsureCalendarTransitions(goals, now);
    }

    /// <returns>True when goal rows were mutated and should be saved.</returns>
    public static bool EnsureCalendarTransitions(IReadOnlyList<DonationGoal> goals, DateTime utcNow)
    {
        // Cyclical Nov / teammate rules stay dormant until one-time goals are done.
        if (!OneTimeGoalsComplete(goals))
        {
            return false;
        }

        var annual = goals.FirstOrDefault(g => g.Key == DonationGoalKeys.LiveOneYear);
        var teammate = goals.FirstOrDefault(g => g.Key == DonationGoalKeys.FundTeammate);
        if (annual is null)
        {
            return false;
        }

        var cycleStart = GetNovemberCycleStart(utcNow);
        if (annual.AnnualYearStart is DateOnly applied && applied >= cycleStart)
        {
            return false;
        }

        // First unlock after one-times: record the cycle date but keep any six-month carryover.
        if (annual.AnnualYearStart is null)
        {
            annual.AnnualYearStart = cycleStart;
            annual.UpdatedAt = utcNow;
            return true;
        }

        // Subsequent November: force Live for one year and reset that year's progress.
        annual.ProgressCents = 0;
        annual.IsCompleted = false;
        annual.CompletedAt = null;
        annual.AnnualYearStart = cycleStart;
        annual.UpdatedAt = utcNow;

        if (teammate is not null)
        {
            teammate.CompletionsThisYear = 0;
            teammate.UpdatedAt = utcNow;
            // ProgressCents intentionally preserved across November preemption.
        }

        return true;
    }

    internal static bool OneTimeGoalsComplete(IReadOnlyList<DonationGoal> goals) =>
        goals
            .Where(g => g.Recurrence == DonationGoalRecurrence.OneTime)
            .All(g => g.IsCompleted);

    internal static DonationGoal? ResolveActive(IReadOnlyList<DonationGoal> goals)
    {
        var oneTime = goals
            .Where(g => g.Recurrence == DonationGoalRecurrence.OneTime && !g.IsCompleted)
            .OrderBy(g => g.SortOrder)
            .FirstOrDefault();
        if (oneTime is not null)
        {
            return oneTime;
        }

        var annual = goals.FirstOrDefault(g => g.Key == DonationGoalKeys.LiveOneYear);
        if (annual is not null && !annual.IsCompleted)
        {
            return annual;
        }

        return goals.FirstOrDefault(g => g.Key == DonationGoalKeys.FundTeammate)
            ?? goals.OrderBy(g => g.SortOrder).LastOrDefault();
    }

    internal static DonationGoal? ResolveNext(IReadOnlyList<DonationGoal> goals, DonationGoal? active)
    {
        if (active is null)
        {
            return null;
        }

        return active.Key switch
        {
            DonationGoalKeys.LaunchAppStore => goals.FirstOrDefault(g => g.Key == DonationGoalKeys.LiveSixMonths),
            DonationGoalKeys.LiveSixMonths => goals.FirstOrDefault(g => g.Key == DonationGoalKeys.LiveOneYear),
            DonationGoalKeys.LiveOneYear => goals.FirstOrDefault(g => g.Key == DonationGoalKeys.FundTeammate),
            DonationGoalKeys.FundTeammate => goals.FirstOrDefault(g => g.Key == DonationGoalKeys.LiveOneYear),
            _ => goals.FirstOrDefault(g => g.SortOrder > active.SortOrder)
        };
    }

    internal static DonationGoalCurrentDto ToDto(DonationGoal? active, DonationGoal? next)
    {
        if (active is null)
        {
            return new DonationGoalCurrentDto
            {
                Title = "Support Liberation Fleet",
                Description = string.Empty,
                RaisedUsd = 0,
                GoalUsd = 0,
                ProgressLabel = "$0/$0",
                Percent = 0,
                NextGoalTitle = next?.Title
            };
        }

        var raisedUsd = (int)(active.ProgressCents / 100);
        var goalUsd = (int)(active.TargetCents / 100);
        var percent = goalUsd <= 0
            ? 0
            : (int)Math.Clamp(Math.Round(100.0 * raisedUsd / goalUsd), 0, 100);

        return new DonationGoalCurrentDto
        {
            Title = active.Title,
            Description = FormatDescription(active),
            RaisedUsd = raisedUsd,
            GoalUsd = goalUsd,
            ProgressLabel = $"${raisedUsd}/${goalUsd}",
            Percent = percent,
            NextGoalTitle = next?.Title
        };
    }

    private static void SeedLiveOneYearFromSixMonths(IReadOnlyList<DonationGoal> goals, DateTime utcNow)
    {
        var sixMonths = goals.FirstOrDefault(g => g.Key == DonationGoalKeys.LiveSixMonths);
        var annual = goals.FirstOrDefault(g => g.Key == DonationGoalKeys.LiveOneYear);
        if (sixMonths is null || annual is null || annual.IsCompleted)
        {
            return;
        }

        // Only the first year goal after six-months: skip if annual already has progress
        // (spillover already applied) or was previously completed / mid-cycle.
        // November resets start ProgressCents at 0 but six-months is already complete, and
        // this method is only invoked from the six-months CompleteGoal path — never again.
        if (annual.ProgressCents > 0 || annual.CompletedAt is not null)
        {
            return;
        }

        // Credit the six-month total into that first year goal (at least 50% of $2000).
        annual.ProgressCents = sixMonths.TargetCents;
        annual.UpdatedAt = utcNow;
    }

    private static string FormatDescription(DonationGoal goal)
    {
        if (goal.Key == DonationGoalKeys.FundTeammate)
        {
            return goal.Description.Replace("{completions}", goal.CompletionsThisYear.ToString(), StringComparison.Ordinal);
        }

        return goal.Description;
    }

    private static void CompleteGoal(DonationGoal goal, DateTime utcNow)
    {
        goal.ProgressCents = goal.TargetCents;
        goal.IsCompleted = true;
        goal.CompletedAt = utcNow;
        goal.UpdatedAt = utcNow;
    }

    /// <summary>
    /// Fundraising year starts each Nov 1 (US Eastern calendar date).
    /// On/after Nov 1 of year Y → cycle start is Y-11-01; before that → (Y-1)-11-01.
    /// </summary>
    public static DateOnly GetNovemberCycleStart(DateTime utcNow)
    {
        var eastern = TimeZoneInfo.ConvertTimeFromUtc(utcNow, Eastern);
        var year = eastern.Month >= 11 ? eastern.Year : eastern.Year - 1;
        return new DateOnly(year, 11, 1);
    }

    private static DateTime NormalizeUtc(DateTime? utcNow)
    {
        if (utcNow is null)
        {
            return DateTime.UtcNow;
        }

        return utcNow.Value.Kind switch
        {
            DateTimeKind.Utc => utcNow.Value,
            DateTimeKind.Local => utcNow.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utcNow.Value, DateTimeKind.Utc)
        };
    }

    private static TimeZoneInfo ResolveEasternTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
    }
}
