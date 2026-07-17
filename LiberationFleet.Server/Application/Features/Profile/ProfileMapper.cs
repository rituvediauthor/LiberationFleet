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
        bool isSurvivalThresholdRecipient,
        decimal donationsPreviousTaxYearUsd = 0m,
        decimal donationsCurrentTaxYearUsd = 0m,
        int previousTaxYear = 0,
        int currentTaxYear = 0)
    {
        return new UserProfileDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            AvatarResourceId = user.AvatarResourceId,
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
            PeopleRepresentedCount = user.PeopleRepresentedCount,
            DisabilityLevel = user.DisabilityLevel,
            NeedsSurvivalAid = user.NeedsSurvivalAid,
            IsSurvivalThresholdRecipient = isSurvivalThresholdRecipient,
            Stats = BuildStats(
                giftStats,
                membership,
                isFinancialMember,
                priorityScore,
                percentBoost,
                donationsPreviousTaxYearUsd,
                donationsCurrentTaxYearUsd,
                previousTaxYear,
                currentTaxYear)
        };
    }

    private static UserProfileStatsDto BuildStats(
        CrewmateGiftStatsDto giftStats,
        CrewMembership? membership,
        bool isFinancialMember,
        decimal priorityScore,
        int percentBoost,
        decimal donationsPreviousTaxYearUsd,
        decimal donationsCurrentTaxYearUsd,
        int previousTaxYear,
        int currentTaxYear)
    {
        return new UserProfileStatsDto
        {
            // Prefer the membership counter: it increments only on emergency responses
            // (recorded gift, already-logged, or cycle split), not every gift given.
            SacrificeCountLastSeason = membership?.EmergencySacrificesThisSeason
                ?? giftStats.SacrificeCountLastSeason,
            AverageMonthlyContributions = giftStats.AverageMonthlyContributions,
            MembershipStatus = isFinancialMember,
            LifetimeContributions = giftStats.LifetimeContributions,
            ReceptionThisYear = giftStats.ReceptionThisYear,
            PercentBoost = percentBoost,
            PriorityScore = (int)Math.Round(priorityScore, MidpointRounding.AwayFromZero),
            DonationsPreviousTaxYearUsd = donationsPreviousTaxYearUsd,
            DonationsCurrentTaxYearUsd = donationsCurrentTaxYearUsd,
            PreviousTaxYear = previousTaxYear,
            CurrentTaxYear = currentTaxYear
        };
    }
}
