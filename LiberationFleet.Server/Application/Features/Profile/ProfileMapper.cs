using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Profile;

public static class ProfileMapper
{
    public static UserProfileDto MapUser(User user, UserGiftStats giftStats, bool isMember, decimal priorityScore)
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
                    Platform = p.PaymentPlatform.Name,
                    Handle = p.Handle
                })
                .ToList(),
            InNeedOfAid = user.InNeedOfAid,
            EmergencyLevel = user.EmergencyLevel,
            NeedsSurvivalAid = user.NeedsSurvivalAid,
            Stats = BuildStats(giftStats, isMember, user.PercentBonus, priorityScore)
        };
    }

    private static UserProfileStatsDto BuildStats(UserGiftStats giftStats, bool isMember, int percentBonus, decimal priorityScore)
    {
        return new UserProfileStatsDto
        {
            SacrificeCount = giftStats.SacrificeCountLastYear,
            AverageMonthlyContributions = Math.Round(giftStats.ContributionsLast3Months / 3m, 2),
            MembershipStatus = isMember,
            LifetimeContributions = giftStats.LifetimeContributions,
            ReceptionLastYear = giftStats.ReceptionLastYear,
            PercentBoost = percentBonus,
            PriorityScore = (int)Math.Round(priorityScore)
        };
    }
}
