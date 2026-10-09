using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Crews;

public sealed class PlaceholderCrewmateResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int UserId { get; init; }

    public static PlaceholderCrewmateResult Succeeded(int userId, string message) =>
        new() { Success = true, Message = message, UserId = userId };

    public static PlaceholderCrewmateResult Failed(string message) =>
        new() { Success = false, Message = message };
}

public sealed class PlaceholderCreateOptions
{
    public bool? InNeedOfAid { get; init; }
    public bool? NeedsSurvivalAid { get; init; }
    /// <summary>
    /// When true, grants honorary membership so the placeholder is treated as a financial member
    /// (member cycle cap) even without contribution history.
    /// </summary>
    public bool? IsFinancialMember { get; init; }
    public decimal? EstimatedMonthlyContribution { get; init; }
    public int? PercentBoost { get; init; }
    public decimal? LifetimeContributionOverride { get; init; }
    public decimal? ReceptionThisYearOverride { get; init; }
    public AidSeasonAccountingDto? SeasonAccounting { get; init; }
}

public class PlaceholderCrewmateService(
    IUserRepository userRepository,
    ICrewMembershipRepository membershipRepository,
    ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
    IMutualAidRepository mutualAidRepository,
    IMutualAidService mutualAidService,
    IGiftRepository giftRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<PlaceholderCrewmateResult> AddPlaceholderAsync(
        int crewId,
        int authorUserId,
        string displayName,
        IReadOnlyList<PaymentPlatformAccountDto> paymentPlatforms,
        int emergencyLevel,
        int peopleRepresentedCount,
        int disabilityLevel,
        IReadOnlyList<string>? identityGroups,
        CancellationToken cancellationToken,
        PlaceholderCreateOptions? options = null)
    {
        var trimmedName = displayName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return PlaceholderCrewmateResult.Failed("Name is required.");
        }

        if (trimmedName.Length > 256)
        {
            return PlaceholderCrewmateResult.Failed("Name must be 256 characters or fewer.");
        }

        if (paymentPlatforms.Count == 0)
        {
            return PlaceholderCrewmateResult.Failed("Register at least one payment platform.");
        }

        if (emergencyLevel is < 0 or > 3)
        {
            return PlaceholderCrewmateResult.Failed("Emergency level must be between 0 and 3.");
        }

        if (peopleRepresentedCount is < 1 or > 99)
        {
            return PlaceholderCrewmateResult.Failed("Number of people represented must be between 1 and 99.");
        }

        if (disabilityLevel is < 0 or > 3)
        {
            return PlaceholderCrewmateResult.Failed("Disability level must be between 0 and 3.");
        }

        if (!IdentityGroupKeys.AreValid(identityGroups))
        {
            return PlaceholderCrewmateResult.Failed("Identity groups contain an unrecognized value.");
        }

        if (options?.PercentBoost is < 0 or > 100)
        {
            return PlaceholderCrewmateResult.Failed("Percent boost must be between 0 and 100.");
        }

        var crew = await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        if (crew is null)
        {
            return PlaceholderCrewmateResult.Failed("Crew not found.");
        }

        var user = new User
        {
            Username = PlaceholderUserDefaults.CreateSyntheticUsername(),
            DisplayName = trimmedName,
            Email = PlaceholderUserDefaults.CreateInternalEmail(),
            PasswordHash = PlaceholderUserDefaults.PasswordHash,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsUnclaimedPlaceholder = true,
            InNeedOfAid = options?.InNeedOfAid ?? true,
            NeedsSurvivalAid = options?.NeedsSurvivalAid ?? false,
            EmergencyLevel = emergencyLevel,
            PeopleRepresentedCount = peopleRepresentedCount,
            DisabilityLevel = disabilityLevel,
            IdentityGroups = IdentityGroupKeys.Serialize(identityGroups)
        };

        var preferredAssigned = false;
        foreach (var platform in paymentPlatforms)
        {
            if (string.IsNullOrWhiteSpace(platform.Handle)
                || (platform.PlatformId <= 0 && string.IsNullOrWhiteSpace(platform.CustomPlatformName)))
            {
                continue;
            }

            CrewPaymentPlatform crewPlatform;
            if (!string.IsNullOrWhiteSpace(platform.CustomPlatformName))
            {
                crewPlatform = await CrewPaymentPlatformService.EnsurePlatformAsync(
                    crewPaymentPlatformRepository,
                    unitOfWork,
                    crewId,
                    platform.CustomPlatformName,
                    cancellationToken);
            }
            else
            {
                var existing = await crewPaymentPlatformRepository.GetByIdAsync(platform.PlatformId, cancellationToken);
                if (existing is null || existing.CrewId != crewId || existing.IsLibraryOfThings)
                {
                    return PlaceholderCrewmateResult.Failed("Invalid payment platform for your crew.");
                }

                crewPlatform = existing;
            }

            var isPreferred = platform.IsPreferred && !preferredAssigned;
            if (isPreferred)
            {
                preferredAssigned = true;
            }

            user.PaymentPlatforms.Add(new UserPaymentPlatform
            {
                CrewPaymentPlatformId = crewPlatform.Id,
                PlatformName = crewPlatform.Name,
                Handle = platform.Handle.Trim(),
                IsPreferred = isPreferred
            });
        }

        if (user.PaymentPlatforms.Count == 0)
        {
            return PlaceholderCrewmateResult.Failed("Register at least one payment platform.");
        }

        if (!preferredAssigned)
        {
            user.PaymentPlatforms.First().IsPreferred = true;
        }

        await userRepository.AddAsync(user, cancellationToken);

        var isFinancialMember = options?.IsFinancialMember == true;
        var membership = new CrewMembership
        {
            User = user,
            CrewId = crewId,
            JoinedAt = DateTime.UtcNow,
            IsPlaceholderMember = true,
            IsHonoraryMember = isFinancialMember,
            IsSeasonReady = true,
            EstimatedMonthlyContribution = options?.EstimatedMonthlyContribution,
            PercentBonus = options?.PercentBoost ?? 0,
            LifetimeContributionOverride = options?.LifetimeContributionOverride,
            ReceptionThisYearOverride = options?.ReceptionThisYearOverride,
            AutoJoinSeasonOnStart = options?.SeasonAccounting?.AutoJoinSeasonOnStart ?? false
        };

        await membershipRepository.AddAsync(membership, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (options?.SeasonAccounting is not null)
        {
            await mutualAidService.ApplyAidSeasonAccountingAsync(
                crewId,
                membership,
                options.SeasonAccounting,
                persistAsDraftWhenNoSeason: true,
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (crew.SeasonStarted)
        {
            await mutualAidService.EnsureMemberInActiveSeasonAsync(crewId, membership, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        membership.CurrentPriorityScore = await mutualAidService.GetPriorityScoreForUserAsync(
            membership.UserId,
            crewId,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var statusLabel = isFinancialMember ? "financial member" : "non-member";
        return PlaceholderCrewmateResult.Succeeded(
            user.Id,
            $"{trimmedName} was added as a placeholder {statusLabel}.");
    }

    public async Task MergePlaceholderIntoClaimantAsync(
        int crewId,
        int placeholderUserId,
        int claimantUserId,
        CancellationToken cancellationToken)
    {
        await giftRepository.ReassignPlaceholderGiftRecipientsAsync(
            crewId,
            placeholderUserId,
            claimantUserId,
            cancellationToken);

        await mutualAidRepository.MergePlaceholderIdentityDataAsync(
            crewId,
            placeholderUserId,
            claimantUserId,
            cancellationToken);

        var placeholderMembership = await membershipRepository.GetMembershipAsync(
            placeholderUserId,
            crewId,
            cancellationToken);
        if (placeholderMembership is not null)
        {
            membershipRepository.Remove(placeholderMembership);
        }

        var placeholderUser = await userRepository.GetByIdWithProfileAsync(placeholderUserId, cancellationToken);
        if (placeholderUser is not null && placeholderUser.IsUnclaimedPlaceholder)
        {
            userRepository.Remove(placeholderUser);
        }

        await mutualAidService.OnCrewmatePriorityChangedAsync(claimantUserId, cancellationToken);
    }
}
