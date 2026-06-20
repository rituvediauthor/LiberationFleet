using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Profile.Commands.UpdateProfile;

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, ProfileOperationResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IGiftRepository _giftRepository;
    private readonly ICrewMembershipRepository _membershipRepository;
    private readonly ICrewPaymentPlatformRepository _crewPaymentPlatformRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMutualAidService _mutualAidService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProfileCommandHandler(
        IUserRepository userRepository,
        IGiftRepository giftRepository,
        ICrewMembershipRepository membershipRepository,
        ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
        ICurrentUserService currentUserService,
        IMutualAidService mutualAidService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _giftRepository = giftRepository;
        _membershipRepository = membershipRepository;
        _crewPaymentPlatformRepository = crewPaymentPlatformRepository;
        _currentUserService = currentUserService;
        _mutualAidService = mutualAidService;
        _unitOfWork = unitOfWork;
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
        if (membership is null)
        {
            return new ProfileOperationResponse { Success = false, Message = "You must be in a crew to manage payment platforms." };
        }

        var previousEmergencyLevel = user.EmergencyLevel;
        var previousInNeedOfAid = user.InNeedOfAid;

        user.Username = request.Username.Trim();
        user.Email = request.Email.Trim();
        user.InNeedOfAid = request.InNeedOfAid;
        user.EmergencyLevel = request.EmergencyLevel;
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
                if (existing is null || existing.CrewId != membership.CrewId)
                {
                    return new ProfileOperationResponse { Success = false, Message = "Invalid payment platform for your crew." };
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

        if (!preferredAssigned && user.PaymentPlatforms.Count > 0)
        {
            user.PaymentPlatforms.First().IsPreferred = true;
        }

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (previousEmergencyLevel != user.EmergencyLevel || previousInNeedOfAid != user.InNeedOfAid)
        {
            await _mutualAidService.OnCrewmatePriorityChangedAsync(userId.Value, cancellationToken);
        }

        var reloaded = await _userRepository.GetByIdWithProfileAsync(userId.Value, cancellationToken);
        UserProfileDto? profile = null;
        if (reloaded is not null)
        {
            var giftStats = await _giftRepository.GetUserGiftStatsAsync(userId.Value, cancellationToken);
            var isFinancialMember = await _mutualAidService.IsFinancialMemberAsync(
                userId.Value,
                membership.CrewId,
                membership,
                cancellationToken);
            var priorityScore = await _mutualAidService.GetPriorityScoreForUserAsync(
                userId.Value,
                membership.CrewId,
                cancellationToken,
                excludeActiveSeasonContributions: membership.IsInSeason);
            profile = ProfileMapper.MapUser(
                reloaded,
                giftStats,
                true,
                isFinancialMember,
                priorityScore,
                reloaded.PercentBonus);
        }

        return new ProfileOperationResponse
        {
            Success = true,
            Message = "Profile updated successfully",
            Profile = profile
        };
    }
}
