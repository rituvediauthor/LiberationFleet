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

    public static decimal CalculateMonthlyGivingCapacity(
        decimal contributionsLast3Months,
        decimal? estimatedMonthlyContribution)
    {
        if (contributionsLast3Months > 0m)
        {
            return Math.Round(contributionsLast3Months / 3m, 2);
        }

        return estimatedMonthlyContribution ?? 0m;
    }

    public static decimal CalculatePriorityScore(
        User user,
        CrewMembership membership,
        bool isFinancialMember,
        decimal crewLifetimeContributions,
        decimal userLifetimeContributions,
        decimal survivalThresholdAmount)
    {
        if (membership.IsOrganizer)
        {
            return -1m;
        }

        if (!user.InNeedOfAid)
        {
            return -2m;
        }

        var membershipBonus = isFinancialMember ? 1m : 0m;
        var percentBonusFactor = 1m - (user.PercentBonus / 100m);

        return (crewLifetimeContributions * user.EmergencyLevel)
            + membershipBonus
            + userLifetimeContributions
            + (survivalThresholdAmount * percentBonusFactor);
    }

    public static bool IsCycleSatisfied(SeasonCycle cycle, decimal effectiveCycleCap) =>
        cycle.CycleReceived >= effectiveCycleCap || cycle.TotalReceptionAmount > effectiveCycleCap;

    public static bool IsSeasonComplete(IEnumerable<SeasonCycle> cycles, Func<SeasonCycle, decimal> effectiveCapResolver) =>
        cycles.All(c => IsCycleSatisfied(c, effectiveCapResolver(c)));
}
