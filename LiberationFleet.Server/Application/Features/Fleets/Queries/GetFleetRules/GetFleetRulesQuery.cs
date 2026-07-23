using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetRules;

public record GetFleetRulesQuery() : IRequest<FleetRuleListResponse>;

public class GetFleetRulesQueryHandler(
    ICurrentUserService currentUser,
    IFleetRepository fleetRepository) : IRequestHandler<GetFleetRulesQuery, FleetRuleListResponse>
{
    public async Task<FleetRuleListResponse> Handle(GetFleetRulesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FleetRuleListResponse { Success = false, Message = "Unauthorized." };
        }

        var fleet = await fleetRepository.GetFleetForUserAsync(currentUser.UserId.Value, cancellationToken);
        if (fleet is null)
        {
            return new FleetRuleListResponse { Success = false, Message = "You are not in a fleet." };
        }

        var rules = await fleetRepository.GetRulesAsync(fleet.Id, cancellationToken);
        return new FleetRuleListResponse
        {
            Success = true,
            Message = "Rules loaded.",
            Items = rules.Select(FleetMapper.MapRule).ToList()
        };
    }
}
