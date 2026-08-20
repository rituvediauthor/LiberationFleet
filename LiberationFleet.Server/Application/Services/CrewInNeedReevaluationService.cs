using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;

namespace LiberationFleet.Server.Application.Services;

public class CrewInNeedReevaluationService(
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    IGiftRepository giftRepository,
    ICrewRepository crewRepository,
    IMutualAidService mutualAidService,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Re-applies in-need threshold rules for every active crewmate after the threshold changes.
    /// </summary>
    public async Task ReevaluateCrewAsync(int crewId, CancellationToken cancellationToken = default)
    {
        var members = await membershipRepository.GetActiveMembersByCrewIdAsync(crewId, cancellationToken);
        foreach (var member in members)
        {
            var user = await userRepository.GetByIdWithProfileAsync(member.UserId, cancellationToken);
            if (user is null)
            {
                continue;
            }

            var wasInNeed = user.InNeedOfAid;
            var changed = await CrewInNeedService.ApplyInNeedDefaultAsync(
                member.UserId,
                userRepository,
                giftRepository,
                crewRepository,
                membershipRepository,
                unitOfWork,
                cancellationToken);

            if (!changed)
            {
                continue;
            }

            var reloaded = await userRepository.GetByIdWithProfileAsync(member.UserId, cancellationToken);
            if (reloaded is null || reloaded.InNeedOfAid == wasInNeed)
            {
                continue;
            }

            await mutualAidService.OnInNeedOfAidChangedAsync(
                member.UserId,
                reloaded.InNeedOfAid,
                cancellationToken);
        }
    }
}
