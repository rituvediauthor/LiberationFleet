using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetStatus;

public record GetFleetStatusQuery : IRequest<FleetMembershipStatusDto>;

public class GetFleetStatusQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IFleetRepository fleetRepository,
    IUserFleetRuleAcceptanceRepository acceptanceRepository) : IRequestHandler<GetFleetStatusQuery, FleetMembershipStatusDto>
{
    public async Task<FleetMembershipStatusDto> Handle(GetFleetStatusQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FleetMembershipStatusDto();
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return new FleetMembershipStatusDto();
        }

        var crew = await crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new FleetMembershipStatusDto
            {
                AllowCrossCrewGiving = crew?.AllowCrossCrewGiving ?? false
            };
        }

        var acceptance = await acceptanceRepository.GetAsync(currentUser.UserId.Value, fleet.Id, cancellationToken);
        var publicRules = await fleetRepository.GetPublicRulesAsync(fleet.Id, cancellationToken);
        var needsRuleAcceptance = acceptance is null && publicRules.Count > 0;

        return new FleetMembershipStatusDto
        {
            HasFleet = true,
            FleetId = fleet.Id,
            FleetName = fleet.Name,
            AllowCrossCrewGiving = crew?.AllowCrossCrewGiving ?? false,
            JoinCode = fleet.JoinCode,
            LibraryOfThingsEnabled = fleet.LibraryOfThingsEnabled,
            NeedsRuleAcceptance = needsRuleAcceptance
        };
    }
}
