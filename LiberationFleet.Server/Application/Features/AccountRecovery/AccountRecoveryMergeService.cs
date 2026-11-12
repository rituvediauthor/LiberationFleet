using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Fleets;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Security;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.AccountRecovery;

public sealed class AccountRecoveryMergeResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    public static AccountRecoveryMergeResult Ok(string message = "Account recovered.") =>
        new() { Success = true, Message = message };

    public static AccountRecoveryMergeResult Fail(string message) =>
        new() { Success = false, Message = message };
}

/// <summary>
/// Absorbs <paramref name="newUserId"/> into <paramref name="oldUserId"/>:
/// new content ownership moves to old; old keeps login identity; encryption identity becomes new's keys;
/// new account is soft-deleted.
/// </summary>
public class AccountRecoveryMergeService(
    IUserRepository userRepository,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IGiftRepository giftRepository,
    IMutualAidRepository mutualAidRepository,
    IMutualAidService mutualAidService,
    IAccountRecoveryRepository accountRecoveryRepository,
    LibraryMemberCleanupService libraryMemberCleanupService,
    EmptyCrewCleanupService emptyCrewCleanupService,
    FleetMembershipService fleetMembershipService,
    ContentTenureService contentTenureService,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
{
    public async Task<AccountRecoveryMergeResult> MergeAsync(
        int crewId,
        int newUserId,
        int oldUserId,
        CancellationToken cancellationToken = default)
    {
        if (newUserId == oldUserId)
        {
            return AccountRecoveryMergeResult.Fail("Cannot recover an account into itself.");
        }

        var newUser = await userRepository.GetByIdWithProfileAsync(newUserId, cancellationToken);
        var oldUser = await userRepository.GetByIdWithProfileAsync(oldUserId, cancellationToken);
        if (newUser is null || oldUser is null)
        {
            return AccountRecoveryMergeResult.Fail("One of the accounts was not found.");
        }

        if (!newUser.IsActive || !oldUser.IsActive
            || newUser.IsUnclaimedPlaceholder
            || oldUser.IsUnclaimedPlaceholder)
        {
            return AccountRecoveryMergeResult.Fail("Both accounts must be active non-placeholder users.");
        }

        var newMembership = await membershipRepository.GetMembershipAsync(newUserId, crewId, cancellationToken);
        var oldMembership = await membershipRepository.GetMembershipAsync(oldUserId, crewId, cancellationToken);
        if (newMembership is null || newMembership.LeftAt is not null || newMembership.IsBanned
            || oldMembership is null || oldMembership.LeftAt is not null || oldMembership.IsBanned)
        {
            return AccountRecoveryMergeResult.Fail("Both accounts must be active members of the same crew.");
        }

        var crew = await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        var seasonStart = crew?.CurrentSeasonStartDate;
        var newStats = await giftRepository.GetCrewmateGiftStatsAsync(newUserId, crewId, seasonStart, cancellationToken);
        var oldStats = await giftRepository.GetCrewmateGiftStatsAsync(oldUserId, crewId, seasonStart, cancellationToken);

        // 1. Drop old avatar/location ciphertext (personal). DMs are preserved.
        await accountRecoveryRepository.DropUndecryptablePersonalContentAsync(oldUserId, cancellationToken);

        // 2. Adopt new encryption identity on the surviving user id.
        await accountRecoveryRepository.TransferEncryptionIdentityAsync(newUserId, oldUserId, cancellationToken);

        // 3. Ownership remaps (new → old).
        await accountRecoveryRepository.ReassignGiftRolesAsync(newUserId, oldUserId, cancellationToken);
        await mutualAidRepository.MergePlaceholderIdentityDataAsync(crewId, newUserId, oldUserId, cancellationToken);
        await accountRecoveryRepository.MergeDirectMessagesAsync(newUserId, oldUserId, cancellationToken);
        await accountRecoveryRepository.ReassignAuthoredContentAsync(newUserId, oldUserId, cancellationToken);
        await accountRecoveryRepository.MergeFriendshipsAndBlocksAsync(newUserId, oldUserId, cancellationToken);
        await accountRecoveryRepository.ReassignLibraryOwnershipAsync(newUserId, oldUserId, cancellationToken);
        await accountRecoveryRepository.ReassignNotificationsAndPrefsAsync(newUserId, oldUserId, cancellationToken);
        await accountRecoveryRepository.ReassignMiscOwnershipAsync(newUserId, oldUserId, cancellationToken);

        // 4. Crew membership: fold new onto old, then remove new.
        oldMembership.LifetimeContributionOverride =
            oldStats.LifetimeContributions + newStats.LifetimeContributions;
        oldMembership.ReceptionThisYearOverride =
            oldStats.ReceptionThisYear + newStats.ReceptionThisYear;
        if (newMembership.IsInSeason || newMembership.IsSeasonReady)
        {
            oldMembership.IsInSeason = true;
            oldMembership.IsSeasonReady = true;
            oldMembership.GivingSeasonJoinedAt ??=
                newMembership.GivingSeasonJoinedAt ?? seasonStart ?? DateTime.UtcNow;
        }

        if (newMembership.AutoJoinSeasonOnStart)
        {
            oldMembership.AutoJoinSeasonOnStart = true;
        }

        if (oldMembership.EstimatedMonthlyContribution is null
            && newMembership.EstimatedMonthlyContribution is decimal estimate)
        {
            oldMembership.EstimatedMonthlyContribution = estimate;
        }

        InheritMissingPaymentPlatforms(newUser, oldUser);
        if (string.IsNullOrWhiteSpace(oldUser.AvatarResourceId)
            && !string.IsNullOrWhiteSpace(newUser.AvatarResourceId))
        {
            oldUser.AvatarResourceId = newUser.AvatarResourceId;
        }

        InheritMissingAidProfileFields(newUser, oldUser);

        await libraryMemberCleanupService.CleanupForDepartingMemberAsync(crewId, newUserId, cancellationToken);
        await contentTenureService.OnLeftCrewAsync(newUserId, crewId, cancellationToken);
        membershipRepository.Remove(newMembership);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 5. Fleet membership for the discarded account.
        var newFleetMembership = await fleetRepository.GetFleetMembershipForUserAsync(newUserId, cancellationToken);
        if (newFleetMembership is not null)
        {
            await contentTenureService.PauseFleetAsync(newUserId, newFleetMembership.FleetId, cancellationToken);
            await fleetMembershipService.ClearFleetMembershipAsync(newUserId, newFleetMembership.FleetId, cancellationToken);
        }

        // 6. Soft-delete the stripped new account (login identity of old survives).
        newUser.Username = $"deleted{newUserId}";
        newUser.Email = $"deleted-{newUserId}@deleted.invalid";
        newUser.PasswordHash = passwordHasher.Hash(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
        newUser.IsActive = false;
        newUser.AvatarResourceId = null;
        newUser.SettingsLockPasswordHash = null;
        newUser.LockSettingsWithPassword = false;
        newUser.TwoFactorEnabled = false;
        newUser.NeedsSurvivalAid = false;
        newUser.InNeedOfAid = false;
        newUser.IdentityGroups = null;
        newUser.LocationNonce = null;
        newUser.LocationCiphertext = null;
        newUser.LocationKeyVersion = null;
        foreach (var platform in newUser.PaymentPlatforms.ToList())
        {
            newUser.PaymentPlatforms.Remove(platform);
        }

        SecurityStampHelper.Bump(newUser);
        await userRepository.UpdateAsync(newUser, cancellationToken);
        await userRepository.UpdateAsync(oldUser, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emptyCrewCleanupService.TryCleanupIfNoActiveMembersAsync(crewId, cancellationToken);
        await mutualAidService.OnCrewmatePriorityChangedAsync(oldUserId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return AccountRecoveryMergeResult.Ok("Account recovered. You are now signed in as your recovered account.");
    }

    private static void InheritMissingAidProfileFields(User fromUser, User toUser)
    {
        if (toUser.InNeedOfAid && !fromUser.InNeedOfAid)
        {
            toUser.InNeedOfAid = false;
        }

        if (!toUser.NeedsSurvivalAid && fromUser.NeedsSurvivalAid)
        {
            toUser.NeedsSurvivalAid = true;
        }

        if (toUser.EmergencyLevel == 0 && fromUser.EmergencyLevel != 0)
        {
            toUser.EmergencyLevel = fromUser.EmergencyLevel;
        }

        if (toUser.DisabilityLevel == 0 && fromUser.DisabilityLevel != 0)
        {
            toUser.DisabilityLevel = fromUser.DisabilityLevel;
        }

        if (toUser.PeopleRepresentedCount == 1 && fromUser.PeopleRepresentedCount != 1)
        {
            toUser.PeopleRepresentedCount = fromUser.PeopleRepresentedCount;
        }
    }

    private static void InheritMissingPaymentPlatforms(User fromUser, User toUser)
    {
        if (fromUser.PaymentPlatforms.Count == 0)
        {
            return;
        }

        var toPlatformIds = toUser.PaymentPlatforms
            .Where(p => p.CrewPaymentPlatformId.HasValue)
            .Select(p => p.CrewPaymentPlatformId!.Value)
            .ToHashSet();
        var toNames = toUser.PaymentPlatforms
            .Select(p => p.PlatformName?.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.ToLowerInvariant())
            .ToHashSet();
        var toHasPreferred = toUser.PaymentPlatforms.Any(p => p.IsPreferred);

        foreach (var platform in fromUser.PaymentPlatforms.ToList())
        {
            if (platform.CrewPaymentPlatform?.IsLibraryOfThings == true
                || string.IsNullOrWhiteSpace(platform.Handle))
            {
                continue;
            }

            var platformName = platform.PlatformName?.Trim() ?? platform.CrewPaymentPlatform?.Name?.Trim();
            var alreadyHas = (platform.CrewPaymentPlatformId is int platformId && toPlatformIds.Contains(platformId))
                || (!string.IsNullOrWhiteSpace(platformName)
                    && toNames.Contains(platformName.ToLowerInvariant()));
            if (alreadyHas)
            {
                continue;
            }

            fromUser.PaymentPlatforms.Remove(platform);
            platform.UserId = toUser.Id;
            platform.IsPreferred = !toHasPreferred && platform.IsPreferred;
            toUser.PaymentPlatforms.Add(platform);
            if (platform.CrewPaymentPlatformId is int movedId)
            {
                toPlatformIds.Add(movedId);
            }

            if (!string.IsNullOrWhiteSpace(platformName))
            {
                toNames.Add(platformName.ToLowerInvariant());
            }

            if (platform.IsPreferred)
            {
                toHasPreferred = true;
            }
        }
    }
}
