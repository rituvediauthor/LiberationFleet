using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Application.Services;
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
        CancellationToken cancellationToken)
    {
        var trimmedName = displayName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return PlaceholderCrewmateResult.Failed("Name is required.");
        }

        if (paymentPlatforms.Count == 0)
        {
            return PlaceholderCrewmateResult.Failed("Register at least one payment platform.");
        }

        if (await userRepository.IsUsernameTakenByOtherUserAsync(trimmedName, 0, cancellationToken))
        {
            return PlaceholderCrewmateResult.Failed("That name is already in use. Choose a different name.");
        }

        var crew = await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        if (crew is null)
        {
            return PlaceholderCrewmateResult.Failed("Crew not found.");
        }

        var user = new User
        {
            Username = trimmedName,
            Email = PlaceholderUserDefaults.CreateInternalEmail(),
            PasswordHash = PlaceholderUserDefaults.PasswordHash,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsUnclaimedPlaceholder = true,
            InNeedOfAid = true
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
                if (existing is null || existing.CrewId != crewId)
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

        var membership = new CrewMembership
        {
            User = user,
            CrewId = crewId,
            JoinedAt = DateTime.UtcNow,
            IsPlaceholderMember = true,
            IsSeasonReady = true
        };

        await membershipRepository.AddAsync(membership, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (crew.SeasonStarted)
        {
            await mutualAidService.EnsureMemberInActiveSeasonAsync(crewId, membership, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return PlaceholderCrewmateResult.Succeeded(user.Id, $"{trimmedName} was added as a non-member.");
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
