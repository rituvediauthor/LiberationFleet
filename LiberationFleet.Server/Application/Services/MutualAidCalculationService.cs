using LiberationFleet.Server.Domain;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Services;

public sealed record PriorityScoreBreakdown(
    decimal Score,
    decimal CrewLifetimeContributions,
    int EmergencyLevel,
    decimal MembershipBonus,
    decimal UserLifetimeContributions,
    decimal SurvivalThresholdAmount,
    decimal BaseScore,
    int PeopleRepresentedCount,
    int DisabilityLevel,
    int TargetedMinorityGroupCount,
    int PriorityMultiplier,
    int PercentBoost,
    decimal SacrificeBonusFactor,
    bool IsFinancialMember);

public static class MutualAidCalculationService
{
    /// <summary>
    /// Rounds a needed/target money amount up to the next whole dollar. Zero and negatives stay 0.
    /// </summary>
    public static decimal CeilingToWholeDollar(decimal amount)
    {
        if (amount <= 0m)
        {
            return 0m;
        }

        return Math.Ceiling(amount);
    }

    public static decimal GetMemberCycleCap(Crew crew, decimal totalMonthlyContributions) =>
        CeilingToWholeDollar(
            crew.MemberCycleCapMode == CycleCapMode.Fixed
                ? crew.MemberCycleCapFixedAmount
                : totalMonthlyContributions * crew.MemberCycleCapMultiplier);

    public static decimal GetNonMemberCycleCap(Crew crew, decimal totalMonthlyContributions) =>
        CeilingToWholeDollar(
            crew.NonMemberCycleCapMode == CycleCapMode.Fixed
                ? crew.NonMemberCycleCapFixedAmount
                : totalMonthlyContributions * crew.NonMemberCycleCapMultiplier);

    public static decimal GetTotalMonthlyContributions(IEnumerable<decimal> estimatedMonthlyContributions) =>
        estimatedMonthlyContributions.Sum();

    public static decimal GetSurvivalThresholdAmount(decimal totalMonthlyContributions, int thresholdRecipientCount)
    {
        if (thresholdRecipientCount <= 0)
        {
            return 0m;
        }

        return CeilingToWholeDollar(totalMonthlyContributions / 2m / thresholdRecipientCount);
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

    /// <summary>
    /// The three most recently completed calendar months (excludes the current month).
    /// </summary>
    public static IReadOnlyList<(int Year, int Month)> GetPastThreeCalendarMonths(DateTime utcNow)
    {
        var current = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return
        [
            (current.AddMonths(-3).Year, current.AddMonths(-3).Month),
            (current.AddMonths(-2).Year, current.AddMonths(-2).Month),
            (current.AddMonths(-1).Year, current.AddMonths(-1).Month)
        ];
    }

    /// <summary>
    /// Financial contributions for one completed calendar month. Empty months before
    /// joining the giving season use the estimate; empty months on or after joining are zero.
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
    /// Three-month contribution average over completed months, using join-month fill rules.
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
        "Last three completed calendar months of gifts, excluding Library of Things; emergency aid is included. The current month is not counted. Months before joining the season use your estimate; completed months after joining with no gifts count as $0.";

    public static PriorityScoreBreakdown CalculatePriorityScoreBreakdown(
        User user,
        CrewMembership membership,
        bool isFinancialMember,
        decimal crewLifetimeContributions,
        decimal userLifetimeContributions,
        decimal survivalThresholdAmount,
        bool demoteOrganizerToLastPlace = true)
    {
        var membershipBonus = isFinancialMember ? 1m : 0m;
        var emergencyLevel = user.EmergencyLevel;
        var peopleRepresentedCount = user.PeopleRepresentedCount;
        var disabilityLevel = user.DisabilityLevel;
        var targetedMinorityGroupCount = IdentityGroupKeys.Parse(user.IdentityGroups).Count;
        var percentBoost = membership.PercentBonus;

        // Organizers receive concentrated cycle aid last when in need: fixed score of -1
        // sorts after all positive scores. Library of Things skips this (demoteOrganizerToLastPlace: false).
        if (demoteOrganizerToLastPlace && membership.IsOrganizer)
        {
            return new PriorityScoreBreakdown(
                Score: -1m,
                CrewLifetimeContributions: crewLifetimeContributions,
                EmergencyLevel: emergencyLevel,
                MembershipBonus: membershipBonus,
                UserLifetimeContributions: userLifetimeContributions,
                SurvivalThresholdAmount: survivalThresholdAmount,
                BaseScore: -1m,
                PeopleRepresentedCount: peopleRepresentedCount,
                DisabilityLevel: disabilityLevel,
                TargetedMinorityGroupCount: targetedMinorityGroupCount,
                PriorityMultiplier: -1,
                PercentBoost: percentBoost,
                SacrificeBonusFactor: 1m + (percentBoost / 100m),
                IsFinancialMember: isFinancialMember);
        }

        var baseScore = (crewLifetimeContributions * emergencyLevel)
            + membershipBonus
            + userLifetimeContributions
            + survivalThresholdAmount;

        // Always at least 1 so dependents+disability+groups of 0 cannot zero the score.
        var priorityMultiplier = peopleRepresentedCount
            + disabilityLevel
            + targetedMinorityGroupCount
            + 1;
        var sacrificeBonusFactor = 1m + (percentBoost / 100m);
        var score = baseScore * priorityMultiplier * sacrificeBonusFactor;

        return new PriorityScoreBreakdown(
            Score: score,
            CrewLifetimeContributions: crewLifetimeContributions,
            EmergencyLevel: emergencyLevel,
            MembershipBonus: membershipBonus,
            UserLifetimeContributions: userLifetimeContributions,
            SurvivalThresholdAmount: survivalThresholdAmount,
            BaseScore: baseScore,
            PeopleRepresentedCount: peopleRepresentedCount,
            DisabilityLevel: disabilityLevel,
            TargetedMinorityGroupCount: targetedMinorityGroupCount,
            PriorityMultiplier: priorityMultiplier,
            PercentBoost: percentBoost,
            SacrificeBonusFactor: sacrificeBonusFactor,
            IsFinancialMember: isFinancialMember);
    }

    public static decimal CalculatePriorityScore(
        User user,
        CrewMembership membership,
        bool isFinancialMember,
        decimal crewLifetimeContributions,
        decimal userLifetimeContributions,
        decimal survivalThresholdAmount,
        bool demoteOrganizerToLastPlace = true) =>
        CalculatePriorityScoreBreakdown(
            user,
            membership,
            isFinancialMember,
            crewLifetimeContributions,
            userLifetimeContributions,
            survivalThresholdAmount,
            demoteOrganizerToLastPlace).Score;


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

        return CeilingToWholeDollar(Math.Max(0m, effectiveCycleCap - cycle.CycleReceived));
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
