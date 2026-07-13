using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetRule;

public record GetFleetRuleQuery(int RuleId) : IRequest<FleetRuleDetailResponse>;

public class GetFleetRuleQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository) : IRequestHandler<GetFleetRuleQuery, FleetRuleDetailResponse>
{
    public async Task<FleetRuleDetailResponse> Handle(GetFleetRuleQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FleetRuleDetailResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return new FleetRuleDetailResponse { Success = false, Message = "You are not in a crew." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new FleetRuleDetailResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        var rule = await fleetRepository.GetRuleByIdAsync(request.RuleId, cancellationToken);
        if (rule is null || rule.FleetId != fleet.Id)
        {
            return new FleetRuleDetailResponse { Success = false, Message = "Rule not found." };
        }

        return new FleetRuleDetailResponse
        {
            Success = true,
            Message = "Rule loaded.",
            Rule = FleetMapper.MapRule(rule)
        };
    }
}
