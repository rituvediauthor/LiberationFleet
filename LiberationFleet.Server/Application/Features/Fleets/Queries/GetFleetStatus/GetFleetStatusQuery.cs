using System.Text.Json;
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

        var publicRules = await fleetRepository.GetPublicRulesAsync(fleet.Id, cancellationToken);
        var requiredRuleIds = publicRules.Select(r => r.Id).OrderBy(id => id).ToList();
        var acceptance = await acceptanceRepository.GetAsync(currentUser.UserId.Value, fleet.Id, cancellationToken);
        var needsRuleAcceptance = publicRules.Count > 0 && !HasAcceptedCurrentRules(acceptance?.AcceptedRuleIdsJson, requiredRuleIds);

        return new FleetMembershipStatusDto
        {
            HasFleet = true,
            FleetId = fleet.Id,
            FleetName = fleet.Name,
            AllowCrossCrewGiving = crew?.AllowCrossCrewGiving ?? false,
            JoinCode = fleet.JoinCode,
            LibraryOfThingsEnabled = fleet.LibraryOfThingsEnabled,
            NeedsRuleAcceptance = needsRuleAcceptance,
            ImageResourceId = fleet.ImageResourceId
        };
    }

    private static bool HasAcceptedCurrentRules(string? acceptedJson, IReadOnlyList<int> requiredRuleIds)
    {
        if (requiredRuleIds.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(acceptedJson))
        {
            return false;
        }

        try
        {
            var accepted = JsonSerializer.Deserialize<List<int>>(acceptedJson) ?? [];
            return accepted.OrderBy(id => id).SequenceEqual(requiredRuleIds);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
