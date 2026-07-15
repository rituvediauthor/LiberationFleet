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
    private readonly IAppDonationRepository _donationRepository;

    public GetMyProfileQueryHandler(
        IUserRepository userRepository,
        IGiftRepository giftRepository,
        ICrewMembershipRepository membershipRepository,
        IMutualAidRepository mutualAidRepository,
        IMutualAidService mutualAidService,
        ICurrentUserService currentUserService,
        IAppDonationRepository donationRepository)
    {
        _userRepository = userRepository;
        _giftRepository = giftRepository;
        _membershipRepository = membershipRepository;
        _mutualAidRepository = mutualAidRepository;
        _mutualAidService = mutualAidService;
        _currentUserService = currentUserService;
        _donationRepository = donationRepository;
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
            var crew = membership.Crew
                ?? await _mutualAidRepository.GetCrewAsync(membership.CrewId, cancellationToken);
            isSurvivalRecipient = crew?.AllowSurvivalThresholds == true
                && unsatisfiedThresholds.Any(t => t.UserId == userId.Value);
        }

        var now = DateTime.UtcNow;
        var currentYear = now.Year;
        var previousYear = currentYear - 1;
        var currentStart = new DateTime(currentYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var previousStart = new DateTime(previousYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextStart = new DateTime(currentYear + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var currentDonations = await _donationRepository.SumCompletedUsdForUserInRangeAsync(
            userId.Value, currentStart, nextStart, cancellationToken);
        var previousDonations = await _donationRepository.SumCompletedUsdForUserInRangeAsync(
            userId.Value, previousStart, currentStart, cancellationToken);

        return ProfileMapper.MapUser(
            user,
            giftStats,
            membership,
            isFinancialMember,
            priorityScore,
            user.PercentBonus,
            isSurvivalRecipient,
            previousDonations,
            currentDonations,
            previousYear,
            currentYear);
    }
}
