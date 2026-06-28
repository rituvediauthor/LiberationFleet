using LiberationFleet.Server.Application.Common.Interfaces.Persistence;

namespace LiberationFleet.Server.Application.Features.Crews;

public class EmptyCrewCleanupService(
    ICrewMembershipRepository membershipRepository,
    ICrewCleanupRepository crewCleanupRepository)
{
    public async Task TryCleanupIfNoActiveMembersAsync(int crewId, CancellationToken cancellationToken = default)
    {
        var activeMembers = await membershipRepository.GetActiveMembersByCrewIdAsync(crewId, cancellationToken);
        if (activeMembers.Count > 0)
        {
            return;
        }

        await crewCleanupRepository.CleanupCrewExceptGiftsAsync(crewId, cancellationToken);
    }
}
