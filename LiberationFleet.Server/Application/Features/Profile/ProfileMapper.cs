using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Profile;

public static class ProfileMapper
{
    public static UserProfileDto MapUser(User user, UserGiftStats giftStats, bool hasActiveCrewMembership)
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
            Stats = BuildStats(giftStats, hasActiveCrewMembership)
        };
    }

    private static UserProfileStatsDto BuildStats(UserGiftStats giftStats, bool hasActiveCrewMembership)
    {
        return new UserProfileStatsDto
        {
            SacrificeCount = giftStats.SacrificeCountLastYear,
            AverageMonthlyContributions = Math.Round(giftStats.ContributionsLast3Months / 3m, 2),
            MembershipStatus = hasActiveCrewMembership,
            LifetimeContributions = giftStats.LifetimeContributions,
            ReceptionLastYear = giftStats.ReceptionLastYear,
            PercentBoost = 0,
            PriorityScore = 0
        };
    }
}
