using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Queries.GetMyCrewMembership;

public class GetMyCrewMembershipQueryHandler(
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    ICurrentUserService currentUserService) : IRequestHandler<GetMyCrewMembershipQuery, CrewMembershipStatusDto>
{
    public async Task<CrewMembershipStatusDto> Handle(GetMyCrewMembershipQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
        {
            return new CrewMembershipStatusDto();
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(userId.Value, cancellationToken);
        if (membership is null)
        {
            return new CrewMembershipStatusDto { HasCrew = false };
        }

        var crew = membership.Crew;
        var giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
            userId.Value,
            membership.CrewId,
            crew.CurrentSeasonStartDate,
            cancellationToken);
        var utcNow = DateTime.UtcNow;

        return new CrewMembershipStatusDto
        {
            HasCrew = true,
            CrewId = membership.CrewId,
            CrewName = crew.Name,
            JoinCode = crew.JoinCode,
            LibraryOfThingsEnabled = crew.LibraryOfThingsEnabled,
            CanAttachFilesToCrewContent = CrewContentPermissionService.CanAttachFilesToCrewContent(
                crew,
                membership,
                giftStats.LifetimeContributions,
                utcNow),
            CanCreateProposals = CrewContentPermissionService.CanCreateProposals(
                crew,
                membership,
                giftStats.LifetimeContributions,
                utcNow),
            CanExportCrewData = CrewRoleAuthorizationService.CanExportCrewData(membership)
        };
    }
}
