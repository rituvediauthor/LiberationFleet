using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Profile;

public static class ProfileMapper
{
    public static PriorityScoreBreakdownDto ToDto(
        PriorityScoreBreakdown breakdown,
        string? statusReason = null) =>
        new()
        {
            Score = (int)Math.Round(breakdown.Score, MidpointRounding.AwayFromZero),
            CrewLifetimeContributions = breakdown.CrewLifetimeContributions,
            EmergencyLevel = breakdown.EmergencyLevel,
            MembershipBonus = breakdown.MembershipBonus,
            UserLifetimeContributions = breakdown.UserLifetimeContributions,
            SurvivalThresholdAmount = breakdown.SurvivalThresholdAmount,
            BaseScore = breakdown.BaseScore,
            PeopleRepresentedCount = breakdown.PeopleRepresentedCount,
            DisabilityLevel = breakdown.DisabilityLevel,
            TargetedMinorityGroupCount = breakdown.TargetedMinorityGroupCount,
            PriorityMultiplier = breakdown.PriorityMultiplier,
            PercentBoost = breakdown.PercentBoost,
            SacrificeBonusFactor = breakdown.SacrificeBonusFactor,
            IsFinancialMember = breakdown.IsFinancialMember,
            StatusReason = statusReason
        };

    public static string? GivingSeasonStatusReason(CrewMembership membership, User user)
    {
        if (membership.IsOrganizer)
        {
            return "Organizer — last to receive concentrated aid via cycles when in need";
        }

        return user.InNeedOfAid ? null : "Not in need (not in the active concentrated-aid queue)";
    }

    public static string? LibraryOfThingsStatusReason(CrewMembership membership) =>
        membership.IsOrganizer
            ? "Computed as if you were not the organizer (LoT does not use the organizer last-place rule)"
            : null;

    public static UserProfileDto MapUser(
        User user,
        CrewmateGiftStatsDto giftStats,
        CrewMembership? membership,
        bool isFinancialMember,
        decimal priorityScore,
        int percentBoost,
        bool isSurvivalThresholdRecipient,
        bool canToggleInNeedOff = true,
        decimal inNeedToggleThreshold = 0m,
        decimal donationsPreviousTaxYearUsd = 0m,
        decimal donationsCurrentTaxYearUsd = 0m,
        int previousTaxYear = 0,
        int currentTaxYear = 0,
        PriorityScoreBreakdownDto? givingSeasonPriority = null,
        PriorityScoreBreakdownDto? libraryOfThingsPriority = null,
        int libraryPriorityTier = 1,
        decimal libraryPriorityAverage = 0m)
    {
        return new UserProfileDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            EncryptedLocation = string.IsNullOrWhiteSpace(user.LocationCiphertext)
                || string.IsNullOrWhiteSpace(user.LocationNonce)
                ? null
                : new EncryptedLocationDto
                {
                    Nonce = user.LocationNonce!,
                    Ciphertext = user.LocationCiphertext!,
                    KeyVersion = user.LocationKeyVersion ?? 1
                },
            AvatarResourceId = user.AvatarResourceId,
            PaymentPlatforms = user.PaymentPlatforms
                .OrderBy(p => p.Id)
                .Select(p => new PaymentPlatformAccountDto
                {
                    Id = p.Id,
                    PlatformId = p.CrewPaymentPlatformId ?? 0,
                    Platform = p.CrewPaymentPlatform?.Name ?? p.PlatformName,
                    CustomPlatformName = p.CrewPaymentPlatformId is null ? p.PlatformName : null,
                    Handle = p.Handle,
                    IsPreferred = p.IsPreferred
                })
                .ToList(),
            Roles = membership is null ? Array.Empty<string>() : CrewRoleMapper.MapRoles(membership),
            InNeedOfAid = user.InNeedOfAid,
            EmergencyLevel = user.EmergencyLevel,
            PeopleRepresentedCount = user.PeopleRepresentedCount,
            DisabilityLevel = user.DisabilityLevel,
            IdentityGroups = IdentityGroupKeys.Parse(user.IdentityGroups),
            NeedsSurvivalAid = user.NeedsSurvivalAid,
            IsSurvivalThresholdRecipient = isSurvivalThresholdRecipient,
            CanToggleInNeedOff = canToggleInNeedOff,
            InNeedToggleThreshold = inNeedToggleThreshold,
            GivingSeasonPriority = givingSeasonPriority,
            LibraryOfThingsPriority = libraryOfThingsPriority,
            LibraryPriorityTier = libraryPriorityTier,
            LibraryPriorityAverage = libraryPriorityAverage,
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
        // Last season's sacrifices were converted into PercentBonus at season start.
        var sacrificeCountLastSeason = MutualAidCalculationService.GetSacrificeCountFromPercentBonus(percentBoost);
        if (sacrificeCountLastSeason == 0 && membership is null)
        {
            sacrificeCountLastSeason = giftStats.SacrificeCountLastSeason;
        }

        return new UserProfileStatsDto
        {
            SacrificeCountLastSeason = sacrificeCountLastSeason,
            // Live counter: increments only on emergency responses this season.
            SacrificeCountThisSeason = membership?.EmergencySacrificesThisSeason ?? 0,
            AverageMonthlyContributions = giftStats.AverageMonthlyContributions,
            MembershipStatus = isFinancialMember,
            LifetimeContributions = membership?.LifetimeContributionOverride ?? giftStats.LifetimeContributions,
            ReceptionThisYear = membership?.ReceptionThisYearOverride ?? giftStats.ReceptionThisYear,
            PercentBoost = percentBoost,
            PriorityScore = (int)Math.Round(priorityScore, MidpointRounding.AwayFromZero),
            DonationsPreviousTaxYearUsd = donationsPreviousTaxYearUsd,
            DonationsCurrentTaxYearUsd = donationsCurrentTaxYearUsd,
            PreviousTaxYear = previousTaxYear,
            CurrentTaxYear = currentTaxYear
        };
    }
}
