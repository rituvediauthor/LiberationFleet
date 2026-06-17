using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Application.Services;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Profile.Queries.GetMyProfile;

public class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, UserProfileDto?>
{
    private readonly IUserRepository _userRepository;
    private readonly IGiftRepository _giftRepository;
    private readonly ICrewMembershipRepository _membershipRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMutualAidCalculationService _calculationService;

    public GetMyProfileQueryHandler(
        IUserRepository userRepository,
        IGiftRepository giftRepository,
        ICrewMembershipRepository membershipRepository,
        ICurrentUserService currentUserService,
        IMutualAidCalculationService calculationService)
    {
        _userRepository = userRepository;
        _giftRepository = giftRepository;
        _membershipRepository = membershipRepository;
        _currentUserService = currentUserService;
        _calculationService = calculationService;
    }

    public async Task<UserProfileDto?> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
        {
            return null;
        }

        var user = await _userRepository.GetByIdWithProfileAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var giftStats = await _giftRepository.GetUserGiftStatsAsync(userId.Value, cancellationToken);
        var membership = await _membershipRepository.GetActiveMembershipAsync(userId.Value, cancellationToken);

        decimal priorityScore = 0;
        bool isMember = false;
        
        if (membership is not null)
        {
            priorityScore = await _calculationService.CalculatePriorityScoreAsync(userId.Value, membership.CrewId);
            isMember = await _calculationService.IsMemberAsync(userId.Value, membership.CrewId);
        }

        return ProfileMapper.MapUser(user, giftStats, isMember, priorityScore);
    }
}
