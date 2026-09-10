using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain;
using LiberationFleet.Server.Domain.Entities;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Profile.Commands.UpdateProfile;

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, ProfileOperationResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IGiftRepository _giftRepository;
    private readonly ICrewMembershipRepository _membershipRepository;
    private readonly ICrewRepository _crewRepository;
    private readonly ICrewPaymentPlatformRepository _crewPaymentPlatformRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMutualAidService _mutualAidService;
    private readonly IMutualAidRepository _mutualAidRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly LibraryPriorityTierService _priorityTierService;

    public UpdateProfileCommandHandler(
        IUserRepository userRepository,
        IGiftRepository giftRepository,
        ICrewMembershipRepository membershipRepository,
        ICrewRepository crewRepository,
        ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
        ICurrentUserService currentUserService,
        IMutualAidService mutualAidService,
        IMutualAidRepository mutualAidRepository,
        IUnitOfWork unitOfWork,
        LibraryPriorityTierService priorityTierService)
    {
        _userRepository = userRepository;
        _giftRepository = giftRepository;
        _membershipRepository = membershipRepository;
        _crewRepository = crewRepository;
        _crewPaymentPlatformRepository = crewPaymentPlatformRepository;
        _currentUserService = currentUserService;
        _mutualAidService = mutualAidService;
        _mutualAidRepository = mutualAidRepository;
        _unitOfWork = unitOfWork;
        _priorityTierService = priorityTierService;
    }

    public async Task<ProfileOperationResponse> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
        {
            return new ProfileOperationResponse { Success = false, Message = "Unauthorized" };
        }

        var user = await _userRepository.GetByIdWithProfileAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return new ProfileOperationResponse { Success = false, Message = "User not found" };
        }

        if (await _userRepository.IsUsernameTakenByOtherUserAsync(request.Username, userId.Value, cancellationToken))
        {
            return new ProfileOperationResponse { Success = false, Message = "Username is already taken" };
        }

        if (await _userRepository.IsEmailTakenByOtherUserAsync(request.Email, userId.Value, cancellationToken))
        {
            return new ProfileOperationResponse { Success = false, Message = "Email is already registered" };
        }

        var membership = await _membershipRepository.GetActiveMembershipAsync(userId.Value, cancellationToken);

        var previousEmergencyLevel = user.EmergencyLevel;
        var previousInNeedOfAid = user.InNeedOfAid;
        var previousPeopleRepresentedCount = user.PeopleRepresentedCount;
        var previousDisabilityLevel = user.DisabilityLevel;
        var previousNeedsSurvivalAid = user.NeedsSurvivalAid;

        user.Username = request.Username.Trim();
        user.Email = request.Email.Trim();
        if (request.ClearLocation)
        {
            user.LocationNonce = null;
            user.LocationCiphertext = null;
            user.LocationKeyVersion = null;
        }
        else if (request.EncryptedLocation is not null)
        {
            user.LocationNonce = request.EncryptedLocation.Nonce.Trim();
            user.LocationCiphertext = request.EncryptedLocation.Ciphertext.Trim();
            user.LocationKeyVersion = request.EncryptedLocation.KeyVersion;
        }

        user.AvatarResourceId = string.IsNullOrWhiteSpace(request.AvatarResourceId)
            ? null
            : request.AvatarResourceId.Trim();

        if (membership is null)
        {
            user.InNeedOfAid = request.InNeedOfAid;
            user.EmergencyLevel = request.EmergencyLevel;
            user.PeopleRepresentedCount = request.PeopleRepresentedCount;
            user.DisabilityLevel = request.DisabilityLevel;
            user.IdentityGroups = IdentityGroupKeys.Serialize(request.IdentityGroups);
            user.NeedsSurvivalAid = request.NeedsSurvivalAid;

            await _userRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var solo = await _userRepository.GetByIdWithProfileAsync(userId.Value, cancellationToken);
            return new ProfileOperationResponse
            {
                Success = true,
                Message = "Profile updated successfully",
                Profile = solo is null
                    ? null
                    : ProfileMapper.MapUser(
                        solo,
                        new CrewmateGiftStatsDto(),
                        membership: null,
                        isFinancialMember: false,
                        priorityScore: 0,
                        percentBoost: 0,
                        isSurvivalThresholdRecipient: false)
            };
        }

        var crew = await _crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
        var inNeedThreshold = crew?.InNeedDefaultThreshold ?? 0m;
        var monthlyExclLot = await _mutualAidService.GetMonthlyContributionExcludingLotAsync(
            userId.Value,
            membership.CrewId,
            cancellationToken);

        if (CrewInNeedService.IsAtOrBelowInNeedThreshold(monthlyExclLot, inNeedThreshold))
        {
            user.InNeedOfAid = true;
        }
        else
        {
            user.InNeedOfAid = request.InNeedOfAid;
        }

        user.EmergencyLevel = request.EmergencyLevel;
        user.PeopleRepresentedCount = request.PeopleRepresentedCount;
        user.DisabilityLevel = request.DisabilityLevel;
        user.IdentityGroups = IdentityGroupKeys.Serialize(request.IdentityGroups);
        user.NeedsSurvivalAid = request.NeedsSurvivalAid;

        var paymentPlatforms = request.PaymentPlatforms
            .Where(p => !string.IsNullOrWhiteSpace(p.Handle)
                && (p.PlatformId > 0 || !string.IsNullOrWhiteSpace(p.CustomPlatformName)))
            .ToList();

        user.PaymentPlatforms.Clear();
        var preferredAssigned = false;

        foreach (var platform in paymentPlatforms)
        {
            CrewPaymentPlatform crewPlatform;
            if (!string.IsNullOrWhiteSpace(platform.CustomPlatformName))
            {
                crewPlatform = await CrewPaymentPlatformService.EnsurePlatformAsync(
                    _crewPaymentPlatformRepository,
                    _unitOfWork,
                    membership.CrewId,
                    platform.CustomPlatformName,
                    cancellationToken);
            }
            else
            {
                var existing = await _crewPaymentPlatformRepository.GetByIdAsync(platform.PlatformId, cancellationToken);
                if (existing is not null
                    && existing.CrewId == membership.CrewId
                    && !existing.IsLibraryOfThings)
                {
                    crewPlatform = existing;
                }
                else if (!string.IsNullOrWhiteSpace(platform.Platform))
                {
                    // Stale IDs from a previous crew: remount by platform name onto this crew.
                    crewPlatform = await CrewPaymentPlatformService.EnsurePlatformAsync(
                        _crewPaymentPlatformRepository,
                        _unitOfWork,
                        membership.CrewId,
                        platform.Platform,
                        cancellationToken);
                }
                else
                {
                    return new ProfileOperationResponse { Success = false, Message = "Invalid payment platform for your crew." };
                }
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

        if (!preferredAssigned && user.PaymentPlatforms.Count > 0)
        {
            user.PaymentPlatforms.First().IsPreferred = true;
        }

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await CrewInNeedService.ApplyInNeedDefaultAsync(
            userId.Value,
            _userRepository,
            _giftRepository,
            _crewRepository,
            _membershipRepository,
            _unitOfWork,
            cancellationToken);

        var reloaded = await _userRepository.GetByIdWithProfileAsync(userId.Value, cancellationToken);
        if (reloaded is null)
        {
            return new ProfileOperationResponse { Success = false, Message = "User not found." };
        }

        if (previousEmergencyLevel != reloaded.EmergencyLevel
            || previousInNeedOfAid != reloaded.InNeedOfAid
            || previousPeopleRepresentedCount != reloaded.PeopleRepresentedCount
            || previousDisabilityLevel != reloaded.DisabilityLevel)
        {
            if (previousInNeedOfAid != reloaded.InNeedOfAid)
            {
                await _mutualAidService.OnInNeedOfAidChangedAsync(
                    userId.Value,
                    reloaded.InNeedOfAid,
                    cancellationToken);
            }

            await _mutualAidService.OnCrewmatePriorityChangedAsync(userId.Value, cancellationToken);
        }

        if (previousNeedsSurvivalAid != reloaded.NeedsSurvivalAid)
        {
            await _mutualAidService.EnsureCurrentMonthSurvivalThresholdsAsync(userId.Value, cancellationToken);
        }

        UserProfileDto? profile = null;
        if (reloaded is not null)
        {
            var giftStats = await _giftRepository.GetCrewmateGiftStatsAsync(
                userId.Value,
                membership.CrewId,
                membership.Crew?.CurrentSeasonStartDate,
                cancellationToken);
            var isFinancialMember = await _mutualAidService.IsFinancialMemberAsync(
                userId.Value,
                membership.CrewId,
                membership,
                cancellationToken);
            var givingSeasonBreakdown = await _mutualAidService.GetPriorityScoreBreakdownForUserAsync(
                userId.Value,
                membership.CrewId,
                cancellationToken);
            var libraryBreakdown = await _mutualAidService.GetPriorityScoreBreakdownForUserAsync(
                userId.Value,
                membership.CrewId,
                cancellationToken,
                assumeInNeedNonOrganizerForLot: true);
            var priorityScore = libraryBreakdown.Score;
            var tierSummary = await _priorityTierService.GetSummaryForUserAsync(
                userId.Value,
                membership.CrewId,
                cancellationToken);
            var unsatisfiedThresholds = await _mutualAidRepository.GetUnsatisfiedThresholdsAsync(
                membership.CrewId,
                cancellationToken);
            var isSurvivalRecipient = crew?.AllowSurvivalThresholds == true
                && unsatisfiedThresholds.Any(t => t.UserId == userId.Value);
            var toggleThreshold = inNeedThreshold;
            var canToggleOff = CrewInNeedService.CanToggleInNeedOff(monthlyExclLot, toggleThreshold);

            profile = ProfileMapper.MapUser(
                reloaded,
                giftStats,
                membership,
                isFinancialMember,
                priorityScore,
                membership?.PercentBonus ?? 0,
                isSurvivalRecipient,
                canToggleOff,
                toggleThreshold,
                givingSeasonPriority: ProfileMapper.ToDto(
                    givingSeasonBreakdown,
                    ProfileMapper.GivingSeasonStatusReason(membership, reloaded)),
                libraryOfThingsPriority: ProfileMapper.ToDto(
                    libraryBreakdown,
                    ProfileMapper.LibraryOfThingsStatusReason(membership)),
                libraryPriorityTier: tierSummary.ViewerTier,
                libraryPriorityAverage: tierSummary.AverageScore);
        }

        return new ProfileOperationResponse
        {
            Success = true,
            Message = "Profile updated successfully",
            Profile = profile
        };
    }
}
