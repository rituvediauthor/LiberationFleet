using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Domain.Entities;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Profile.Commands.UpdateProfile;

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, ProfileOperationResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IGiftRepository _giftRepository;
    private readonly ICrewMembershipRepository _membershipRepository;
    private readonly IPaymentPlatformRepository _paymentPlatformRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMutualAidService _mutualAidService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProfileCommandHandler(
        IUserRepository userRepository,
        IGiftRepository giftRepository,
        ICrewMembershipRepository membershipRepository,
        IPaymentPlatformRepository paymentPlatformRepository,
        ICurrentUserService currentUserService,
        IMutualAidService mutualAidService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _giftRepository = giftRepository;
        _membershipRepository = membershipRepository;
        _paymentPlatformRepository = paymentPlatformRepository;
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

        var previousEmergencyLevel = user.EmergencyLevel;
        var previousInNeedOfAid = user.InNeedOfAid;

        user.Username = request.Username.Trim();
        user.Email = request.Email.Trim();
        user.InNeedOfAid = request.InNeedOfAid;
        user.EmergencyLevel = request.EmergencyLevel;
        user.NeedsSurvivalAid = request.NeedsSurvivalAid;

        var paymentPlatforms = request.PaymentPlatforms
            .Where(p => p.PlatformId > 0 && !string.IsNullOrWhiteSpace(p.Handle))
            .ToList();

        foreach (var platform in paymentPlatforms)
        {
            if (!await _paymentPlatformRepository.ExistsAsync(platform.PlatformId, cancellationToken))
            {
                return new ProfileOperationResponse { Success = false, Message = "Invalid payment platform." };
            }
        }

        user.PaymentPlatforms.Clear();
        foreach (var platform in paymentPlatforms)
        {
            user.PaymentPlatforms.Add(new UserPaymentPlatform
            {
                PaymentPlatformId = platform.PlatformId,
                Handle = platform.Handle.Trim()
            });
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
            var membership = await _membershipRepository.GetActiveMembershipAsync(userId.Value, cancellationToken);
            var isFinancialMember = membership is not null
                && await _mutualAidService.IsFinancialMemberAsync(userId.Value, membership.CrewId, membership, cancellationToken);
            var priorityScore = membership is not null
                ? await _mutualAidService.GetPriorityScoreForUserAsync(
                    userId.Value,
                    membership.CrewId,
                    cancellationToken,
                    excludeActiveSeasonContributions: membership.IsInSeason)
                : 0m;
            profile = ProfileMapper.MapUser(
                reloaded,
                giftStats,
                membership is not null,
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
