using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Services;

public static class CrewInNeedService
{
    public static async Task ApplyInNeedDefaultAsync(
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
            return;
        }

        var crew = await crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
        if (crew is null)
        {
            return;
        }

        var giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
            userId,
            crew.Id,
            crew.CurrentSeasonStartDate,
            cancellationToken);

        if (giftStats.AverageMonthlyContributions >= crew.InNeedDefaultThreshold)
        {
            return;
        }

        var user = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        if (user is null || user.InNeedOfAid)
        {
            return;
        }

        user.InNeedOfAid = true;
        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
