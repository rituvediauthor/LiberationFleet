using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Profile;

public static class ProfileMapper
{
    public static UserProfileDto MapUser(
        User user,
        CrewmateGiftStatsDto giftStats,
        CrewMembership? membership,
        bool isFinancialMember,
        decimal priorityScore,
        int percentBoost,
        bool isSurvivalThresholdRecipient)
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
                    PlatformId = p.CrewPaymentPlatformId,
                    Platform = p.CrewPaymentPlatform?.Name ?? string.Empty,
                    Handle = p.Handle,
                    IsPreferred = p.IsPreferred
                })
                .ToList(),
            Roles = membership is null ? Array.Empty<string>() : CrewRoleMapper.MapRoles(membership),
            InNeedOfAid = user.InNeedOfAid,
            EmergencyLevel = user.EmergencyLevel,
            NeedsSurvivalAid = user.NeedsSurvivalAid,
            IsSurvivalThresholdRecipient = isSurvivalThresholdRecipient,
            Stats = BuildStats(giftStats, isFinancialMember, priorityScore, percentBoost)
        };
    }

    private static UserProfileStatsDto BuildStats(
        CrewmateGiftStatsDto giftStats,
        bool isFinancialMember,
        decimal priorityScore,
        int percentBoost)
    {
        return new UserProfileStatsDto
        {
            SacrificeCountLastSeason = giftStats.SacrificeCountLastSeason,
            AverageMonthlyContributions = giftStats.AverageMonthlyContributions,
            MembershipStatus = isFinancialMember,
            LifetimeContributions = giftStats.LifetimeContributions,
            ReceptionThisYear = giftStats.ReceptionThisYear,
            PercentBoost = percentBoost,
            PriorityScore = (int)Math.Round(priorityScore, MidpointRounding.AwayFromZero)
        };
    }
}
