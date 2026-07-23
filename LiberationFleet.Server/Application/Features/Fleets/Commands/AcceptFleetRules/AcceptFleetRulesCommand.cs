using System.Text.Json;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Domain.Entities;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Commands.AcceptFleetRules;

public record AcceptFleetRulesCommand(IReadOnlyList<int> AcceptedRuleIds) : IRequest<FleetOperationResponse>;

public class AcceptFleetRulesCommandHandler(
    ICurrentUserService currentUser,
    IFleetRepository fleetRepository,
    IUserFleetRuleAcceptanceRepository acceptanceRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<AcceptFleetRulesCommand, FleetOperationResponse>
{
    public async Task<FleetOperationResponse> Handle(AcceptFleetRulesCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FleetOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var fleet = await fleetRepository.GetFleetForUserAsync(userId, cancellationToken);
        if (fleet is null)
        {
            return new FleetOperationResponse { Success = false, Message = "You are not in a fleet." };
        }

        var publicRules = await fleetRepository.GetPublicRulesAsync(fleet.Id, cancellationToken);
        var requiredRuleIds = publicRules.Select(r => r.Id).OrderBy(id => id).ToList();
        var acceptedRuleIds = request.AcceptedRuleIds.Distinct().OrderBy(id => id).ToList();

        if (!requiredRuleIds.SequenceEqual(acceptedRuleIds))
        {
            return new FleetOperationResponse
            {
                Success = false,
                Message = "You must accept all public rules before viewing the fleet."
            };
        }

        var json = JsonSerializer.Serialize(acceptedRuleIds);
        var existing = await acceptanceRepository.GetAsync(userId, fleet.Id, cancellationToken);
        if (existing is null)
        {
            await acceptanceRepository.AddAsync(new UserFleetRuleAcceptance
            {
                UserId = userId,
                FleetId = fleet.Id,
                AcceptedRuleIdsJson = json,
                AcceptedAt = DateTime.UtcNow
            }, cancellationToken);
        }
        else
        {
            existing.AcceptedRuleIdsJson = json;
            existing.AcceptedAt = DateTime.UtcNow;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new FleetOperationResponse { Success = true, Message = "Fleet rules accepted." };
    }
}
