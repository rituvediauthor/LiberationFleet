using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Commands.SubmitFleetJoinRequest;

public record SubmitFleetJoinRequestCommand(
    int? FleetId,
    string? JoinCode,
    IReadOnlyList<int> AcceptedRuleIds) : IRequest<FleetJoinRequestOperationResponse>;

public class SubmitFleetJoinRequestCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    CrewApplyToFleetProposalService crewApplyToFleetProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<SubmitFleetJoinRequestCommand, FleetJoinRequestOperationResponse>
{
    public async Task<FleetJoinRequestOperationResponse> Handle(
        SubmitFleetJoinRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FleetJoinRequestOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return new FleetJoinRequestOperationResponse
            {
                Success = false,
                Message = "You must be in a crew to request joining a fleet."
            };
        }

        var fleet = !string.IsNullOrWhiteSpace(request.JoinCode)
            ? await fleetRepository.GetByJoinCodeAsync(request.JoinCode.Trim().ToUpperInvariant(), cancellationToken)
            : request.FleetId.HasValue
                ? await fleetRepository.GetByIdAsync(request.FleetId.Value, cancellationToken)
                : null;

        if (fleet is null)
        {
            return new FleetJoinRequestOperationResponse
            {
                Success = false,
                Message = !string.IsNullOrWhiteSpace(request.JoinCode)
                    ? "No fleet found with that join code"
                    : "Fleet not found"
            };
        }

        if (fleet.Privacy == CrewPrivacy.InviteOnly)
        {
            return new FleetJoinRequestOperationResponse
            {
                Success = false,
                Message = PrivacyAccess.InviteOnlyJoinMessage("fleet")
            };
        }

        if (fleet.Privacy == CrewPrivacy.Private && string.IsNullOrWhiteSpace(request.JoinCode))
        {
            return new FleetJoinRequestOperationResponse { Success = false, Message = "Fleet not found." };
        }

        var publicRules = await fleetRepository.GetPublicRulesAsync(fleet.Id, cancellationToken);
        var requiredRuleIds = publicRules.Select(r => r.Id).OrderBy(id => id).ToList();
        var acceptedRuleIds = request.AcceptedRuleIds.Distinct().OrderBy(id => id).ToList();

        if (requiredRuleIds.Count > 0 && !requiredRuleIds.SequenceEqual(acceptedRuleIds))
        {
            return new FleetJoinRequestOperationResponse
            {
                Success = false,
                Message = "You must accept all public rules before requesting to join."
            };
        }

        var result = await crewApplyToFleetProposalService.CreateAsync(
            currentUser.UserId.Value,
            membership.CrewId,
            fleet,
            request.JoinCode,
            acceptedRuleIds,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new FleetJoinRequestOperationResponse
        {
            Success = result.Success,
            Message = result.Message,
            ProposalId = result.ProposalId
        };
    }
}
