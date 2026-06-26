using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Profile.Queries.GetMyProfile;

public class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, UserProfileDto?>
{
    private readonly IUserRepository _userRepository;
    private readonly IGiftRepository _giftRepository;
    private readonly ICrewMembershipRepository _membershipRepository;
    private readonly IMutualAidRepository _mutualAidRepository;
    private readonly IMutualAidService _mutualAidService;
    private readonly ICurrentUserService _currentUserService;

    public GetMyProfileQueryHandler(
        IUserRepository userRepository,
        IGiftRepository giftRepository,
        ICrewMembershipRepository membershipRepository,
        IMutualAidRepository mutualAidRepository,
        IMutualAidService mutualAidService,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _giftRepository = giftRepository;
        _membershipRepository = membershipRepository;
        _mutualAidRepository = mutualAidRepository;
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

        var membership = await _membershipRepository.GetActiveMembershipAsync(userId.Value, cancellationToken);

        var isFinancialMember = false;
        var priorityScore = 0m;
        var giftStats = new CrewmateGiftStatsDto();
        var isSurvivalRecipient = false;

        if (membership is not null)
        {
            giftStats = await _giftRepository.GetCrewmateGiftStatsAsync(
                userId.Value,
                membership.CrewId,
                membership.Crew?.CurrentSeasonStartDate,
                cancellationToken);

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

            var unsatisfiedThresholds = await _mutualAidRepository.GetUnsatisfiedThresholdsAsync(
                membership.CrewId,
                cancellationToken);
            isSurvivalRecipient = unsatisfiedThresholds.Any(t => t.UserId == userId.Value);
        }

        return ProfileMapper.MapUser(
            user,
            giftStats,
            membership,
            isFinancialMember,
            priorityScore,
            user.PercentBonus,
            isSurvivalRecipient);
    }
}
