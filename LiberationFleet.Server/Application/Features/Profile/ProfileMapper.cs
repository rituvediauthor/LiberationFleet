using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Profile;

public static class ProfileMapper
{
    public static UserProfileDto MapUser(
        User user,
        UserGiftStats giftStats,
        bool hasActiveCrewMembership,
        bool isFinancialMember,
        decimal priorityScore,
        int percentBoost)
    {
        return new UserProfileDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            PaymentPlatforms = user.PaymentPlatforms
                .OrderBy(p => p.Id)
                .Select(p => new PaymentPlatformAccountDto
                {
                    Id = p.Id,
                    PlatformId = p.PaymentPlatformId,
                    Platform = p.PaymentPlatform?.Name ?? string.Empty,
                    Handle = p.Handle
                })
                .ToList(),
            InNeedOfAid = user.InNeedOfAid,
            EmergencyLevel = user.EmergencyLevel,
            NeedsSurvivalAid = user.NeedsSurvivalAid,
            Stats = BuildStats(giftStats, isFinancialMember, priorityScore, percentBoost)
        };
    }

    private static UserProfileStatsDto BuildStats(
        UserGiftStats giftStats,
        bool isFinancialMember,
        decimal priorityScore,
        int percentBoost)
    {
        return new UserProfileStatsDto
        {
            SacrificeCount = giftStats.SacrificeCountLastYear,
            AverageMonthlyContributions = Math.Round(giftStats.ContributionsLast3Months / 3m, 2),
            MembershipStatus = isFinancialMember,
            LifetimeContributions = giftStats.LifetimeContributions,
            ReceptionLastYear = giftStats.ReceptionLastYear,
            PercentBoost = percentBoost,
            PriorityScore = (int)Math.Round(priorityScore, MidpointRounding.AwayFromZero)
        };
    }
}
