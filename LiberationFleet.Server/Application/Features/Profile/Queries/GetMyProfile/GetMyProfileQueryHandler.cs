using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Profile.Queries.GetMyProfile;

public class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, UserProfileDto?>
{
    private readonly IUserRepository _userRepository;
    private readonly IGiftRepository _giftRepository;
    private readonly ICrewMembershipRepository _membershipRepository;
    private readonly IMutualAidService _mutualAidService;
    private readonly ICurrentUserService _currentUserService;

    public GetMyProfileQueryHandler(
        IUserRepository userRepository,
        IGiftRepository giftRepository,
        ICrewMembershipRepository membershipRepository,
        IMutualAidService mutualAidService,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _giftRepository = giftRepository;
        _membershipRepository = membershipRepository;
        _mutualAidService = mutualAidService;
        _currentUserService = currentUserService;
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

        var isFinancialMember = false;
        var priorityScore = 0m;
        if (membership is not null)
        {
            isFinancialMember = await _mutualAidService.IsFinancialMemberAsync(
                userId.Value,
                membership.CrewId,
                membership,
                cancellationToken);
            priorityScore = await _mutualAidService.GetPriorityScoreForUserAsync(
                userId.Value,
                membership.CrewId,
                cancellationToken,
                excludeActiveSeasonContributions: membership.IsInSeason);
        }

        return ProfileMapper.MapUser(
            user,
            giftStats,
            membership is not null,
            isFinancialMember,
            priorityScore,
            user.PercentBonus);
    }
}
