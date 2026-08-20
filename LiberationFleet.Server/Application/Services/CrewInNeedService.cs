using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Services;

public static class CrewInNeedService
{
    /// <summary>
    /// True when the LoT-excluded 3-month average is at or below the in-need threshold (forced in-need).
    /// </summary>
    public static bool IsAtOrBelowInNeedThreshold(decimal monthlyContributionExclLot, decimal inNeedThreshold) =>
        monthlyContributionExclLot <= inNeedThreshold;

    /// <summary>
    /// Toggle off allowed only when average exceeds the in-need threshold (LoT excluded).
    /// </summary>
    public static bool CanToggleInNeedOff(decimal monthlyContributionExclLot, decimal inNeedThreshold) =>
        monthlyContributionExclLot > inNeedThreshold;

    /// <summary>
    /// Forces in-need when the crewmate's 3-month average (excluding LoT) is at or below
    /// <see cref="Crew.InNeedDefaultThreshold"/>. Returns true when InNeedOfAid was changed from false to true.
    /// </summary>
    public static async Task<bool> ApplyInNeedDefaultAsync(
        int userId,
        IUserRepository userRepository,
        IGiftRepository giftRepository,
        ICrewRepository crewRepository,
        ICrewMembershipRepository membershipRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken = default)
    {
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return false;
        }

        var crew = await crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
        if (crew is null)
        {
            return false;
        }

        var giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
            userId,
            crew.Id,
            crew.CurrentSeasonStartDate,
            cancellationToken);

        if (!IsAtOrBelowInNeedThreshold(giftStats.AverageMonthlyContributions, crew.InNeedDefaultThreshold))
        {
            return false;
        }

        var user = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        if (user is null || user.InNeedOfAid)
        {
            return false;
        }

        user.InNeedOfAid = true;
        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
