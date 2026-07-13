using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetPublicFleetRules;

public record GetPublicFleetRulesQuery(int? FleetId, string? JoinCode) : IRequest<PublicFleetRulesResponse>;

public class GetPublicFleetRulesQueryHandler(
    IFleetRepository fleetRepository) : IRequestHandler<GetPublicFleetRulesQuery, PublicFleetRulesResponse>
{
    public async Task<PublicFleetRulesResponse> Handle(GetPublicFleetRulesQuery request, CancellationToken cancellationToken)
    {
        var hasJoinCode = !string.IsNullOrWhiteSpace(request.JoinCode);
        var fleet = hasJoinCode
            ? await fleetRepository.GetByJoinCodeAsync(request.JoinCode!.Trim().ToUpperInvariant(), cancellationToken)
            : request.FleetId.HasValue
                ? await fleetRepository.GetByIdAsync(request.FleetId.Value, cancellationToken)
                : null;

        if (fleet is null)
        {
            return new PublicFleetRulesResponse
            {
                Success = false,
                Message = hasJoinCode
                    ? "No fleet found with that join code"
                    : "Fleet not found"
            };
        }

        if (fleet.Privacy == CrewPrivacy.InviteOnly
            || (hasJoinCode && !PrivacyAccess.CanDiscoverByJoinCode(fleet.Privacy)))
        {
            return new PublicFleetRulesResponse
            {
                Success = false,
                Message = PrivacyAccess.InviteOnlyJoinMessage("fleet")
            };
        }

        if (!PrivacyAccess.CanDiscoverByBrowse(fleet.Privacy) && !hasJoinCode)
        {
            return new PublicFleetRulesResponse { Success = false, Message = "Fleet not found." };
        }

        var rules = await fleetRepository.GetPublicRulesAsync(fleet.Id, cancellationToken);
        return new PublicFleetRulesResponse
        {
            Success = true,
            Message = "Public rules loaded.",
            FleetId = fleet.Id,
            FleetName = fleet.Name,
            Items = rules.Select(r => new PublicFleetRuleDto
            {
                Id = r.Id,
                Title = r.Title ?? string.Empty,
                Description = r.Description ?? string.Empty
            }).ToList()
        };
    }
}
