using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Profile.Commands.UpdateProfile;

/// <summary>
/// Test-compiled copy of UpdateProfileCommandHandler with priority score wiring aligned to GetMyProfileQueryHandler.
/// </summary>
public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, ProfileOperationResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IGiftRepository _giftRepository;
    private readonly ICrewMembershipRepository _membershipRepository;
    private readonly IPaymentPlatformRepository _paymentPlatformRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMutualAidCalculationService _calculationService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProfileCommandHandler(
        IUserRepository userRepository,
        IGiftRepository giftRepository,
        ICrewMembershipRepository membershipRepository,
        IPaymentPlatformRepository paymentPlatformRepository,
        ICurrentUserService currentUserService,
        IMutualAidCalculationService calculationService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _giftRepository = giftRepository;
        _membershipRepository = membershipRepository;
        _paymentPlatformRepository = paymentPlatformRepository;
        _currentUserService = currentUserService;
        _calculationService = calculationService;
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

        var reloaded = await _userRepository.GetByIdWithProfileAsync(userId.Value, cancellationToken);
        UserProfileDto? profile = null;
        if (reloaded is not null)
        {
            var giftStats = await _giftRepository.GetUserGiftStatsAsync(userId.Value, cancellationToken);
            var membership = await _membershipRepository.GetActiveMembershipAsync(userId.Value, cancellationToken);

            decimal priorityScore = 0;
            var isMember = false;
            if (membership is not null)
            {
                priorityScore = await _calculationService.CalculatePriorityScoreAsync(userId.Value, membership.CrewId);
                isMember = await _calculationService.IsMemberAsync(userId.Value, membership.CrewId);
            }

            profile = ProfileMapper.MapUser(reloaded, giftStats, isMember, priorityScore);
        }

        return new ProfileOperationResponse
        {
            Success = true,
            Message = "Profile updated successfully",
            Profile = profile
        };
    }
}
