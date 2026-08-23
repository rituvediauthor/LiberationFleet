using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Services;

public static class MutualAidCalculationService
{
    public static decimal GetMemberCycleCap(Crew crew, decimal totalMonthlyContributions) =>
        crew.MemberCycleCapMode == CycleCapMode.Fixed
            ? crew.MemberCycleCapFixedAmount
            : totalMonthlyContributions * crew.MemberCycleCapMultiplier;

    public static decimal GetNonMemberCycleCap(Crew crew, decimal totalMonthlyContributions) =>
        crew.NonMemberCycleCapMode == CycleCapMode.Fixed
            ? crew.NonMemberCycleCapFixedAmount
            : totalMonthlyContributions * crew.NonMemberCycleCapMultiplier;

    public static decimal GetTotalMonthlyContributions(IEnumerable<decimal> estimatedMonthlyContributions) =>
        estimatedMonthlyContributions.Sum();

    public static decimal GetSurvivalThresholdAmount(decimal totalMonthlyContributions, int thresholdRecipientCount)
    {
        if (thresholdRecipientCount <= 0)
        {
            return 0m;
        }

        return totalMonthlyContributions / 2m / thresholdRecipientCount;
    }

    public static decimal GetEffectiveMemberCycleCap(decimal seasonStartCap, decimal currentCalculatedCap) =>
        currentCalculatedCap <= seasonStartCap ? currentCalculatedCap : seasonStartCap;

    public static decimal GetEffectiveNonMemberCycleCap(decimal seasonStartCap, decimal currentCalculatedCap) =>
        currentCalculatedCap <= seasonStartCap ? currentCalculatedCap : seasonStartCap;

    /// <summary>
    /// First of the calendar month that counts as this crewmate's giving-season start
    /// for capacity. Joining with fewer than 15 days left in the month rounds up.
    /// </summary>
    public static DateTime GetEffectiveGivingSeasonJoinMonthStart(DateTime joinedAtUtc)
    {
        var year = joinedAtUtc.Year;
        var month = joinedAtUtc.Month;
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var daysLeftIncludingJoinDay = daysInMonth - joinedAtUtc.Day + 1;
        if (daysLeftIncludingJoinDay < 15)
        {
            return new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        }

        return new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    public static IReadOnlyList<(int Year, int Month)> GetPastThreeCalendarMonths(DateTime utcNow)
    {
        var current = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return
        [
            (current.AddMonths(-2).Year, current.AddMonths(-2).Month),
            (current.AddMonths(-1).Year, current.AddMonths(-1).Month),
            (current.Year, current.Month)
        ];
    }

    /// <summary>
    /// Financial contributions for one calendar month. Empty months before joining
    /// the giving season use the estimate; empty months after joining are zero.
    /// </summary>
    public static decimal GetCalendarMonthContribution(
        decimal financialContributionsInMonth,
        DateTime monthStartUtc,
        DateTime? effectiveJoinMonthStartUtc,
        decimal estimatedMonthlyContribution)
    {
        if (financialContributionsInMonth > 0m)
        {
            return financialContributionsInMonth;
        }

        var monthIsBeforeJoin = !effectiveJoinMonthStartUtc.HasValue
            || monthStartUtc < effectiveJoinMonthStartUtc.Value;
        return monthIsBeforeJoin ? estimatedMonthlyContribution : 0m;
    }

    public static decimal AverageMonthlyGivingCapacity(IEnumerable<decimal> monthAmounts)
    {
        var list = monthAmounts as IList<decimal> ?? monthAmounts.ToList();
        if (list.Count == 0)
        {
            return 0m;
        }

        return Math.Round(list.Average(), 2);
    }

    /// <summary>
    /// Three-month contribution average using join-month fill rules.
    /// </summary>
    public static decimal CalculateThreeMonthContributionAverage(
        IReadOnlyList<(int Year, int Month)> months,
        IReadOnlyDictionary<(int Year, int Month), decimal> actualByMonth,
        DateTime? givingSeasonJoinedAtUtc,
        decimal estimatedMonthlyContribution)
    {
        DateTime? effectiveJoinMonthStart = givingSeasonJoinedAtUtc.HasValue
            ? GetEffectiveGivingSeasonJoinMonthStart(givingSeasonJoinedAtUtc.Value)
            : null;

        var monthAmounts = months.Select(month =>
        {
            var monthStart = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var actual = actualByMonth.GetValueOrDefault((month.Year, month.Month));
            return GetCalendarMonthContribution(
                actual,
                monthStart,
                effectiveJoinMonthStart,
                estimatedMonthlyContribution);
        });

        return AverageMonthlyGivingCapacity(monthAmounts);
    }

    public const string MonthlyContributionAverageExplanation =
        "Last three calendar months of gifts, excluding Library of Things; emergency aid is included. Months before joining the season use your estimate; months after joining with no gifts count as $0.";

    public static decimal CalculatePriorityScore(
        User user,
        CrewMembership membership,
        bool isFinancialMember,
        decimal crewLifetimeContributions,
        decimal userLifetimeContributions,
        decimal survivalThresholdAmount,
        bool applyLotCommerceModifiers = false)
    {
        // Organizer / not-in-need demotions apply to Library of Things request
        // ranking only — not to profile display or giving/receiving seasons.
        if (applyLotCommerceModifiers)
        {
            if (membership.IsOrganizer)
            {
                return -1m;
            }

            if (!user.InNeedOfAid)
            {
                return -2m;
            }
        }

        var membershipBonus = isFinancialMember ? 1m : 0m;

        var baseScore = (crewLifetimeContributions * user.EmergencyLevel)
            + membershipBonus
            + userLifetimeContributions
            + survivalThresholdAmount;

        // Always at least 1 so dependents+disability of 0 cannot zero the score.
        var priorityMultiplier = user.PeopleRepresentedCount + user.DisabilityLevel + 1;
        var sacrificeBonusFactor = 1m + (user.PercentBonus / 100m);
        return baseScore * priorityMultiplier * sacrificeBonusFactor;
    }

    public static bool IsCycleSatisfied(SeasonCycle cycle, decimal effectiveCycleCap) =>
        cycle.CycleReceived >= effectiveCycleCap;

    /// <summary>
    /// Cap can shrink or grow with capacity, but never above the value frozen at season start.
    /// </summary>
    public static decimal GetCatchUpAmount(SeasonCycle cycle, decimal effectiveCycleCap)
    {
        if (!cycle.CycleCompleted || cycle.UsesSegmentCap)
        {
            return 0m;
        }

        var endedAt = cycle.CycleCapAtCompletion > 0m
            ? cycle.CycleCapAtCompletion
            : cycle.CycleReceived;

        if (effectiveCycleCap <= endedAt)
        {
            return 0m;
        }

        return Math.Max(0m, effectiveCycleCap - cycle.CycleReceived);
    }

    public static int GetSacrificePercentBonus(int emergencySacrificeCount) =>
        Math.Max(0, emergencySacrificeCount) * 10;

    /// <summary>
    /// Inverse of <see cref="GetSacrificePercentBonus"/>: each emergency sacrifice is worth +10% for the following season.
    /// </summary>
    public static int GetSacrificeCountFromPercentBonus(int percentBonus) =>
        Math.Max(0, percentBonus) / 10;

    public static bool IsSeasonComplete(IEnumerable<SeasonCycle> cycles, Func<SeasonCycle, decimal> effectiveCapResolver) =>
        cycles.All(c => IsCycleSatisfied(c, effectiveCapResolver(c)));
}
