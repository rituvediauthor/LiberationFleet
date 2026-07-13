using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Application.Features.Rules;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Commands.DeleteFleetRule;

public record DeleteFleetRuleCommand(int RuleId) : IRequest<FleetRuleOperationResponse>;

public class DeleteFleetRuleCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    FleetRulesProposalService fleetRulesProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteFleetRuleCommand, FleetRuleOperationResponse>
{
    public async Task<FleetRuleOperationResponse> Handle(DeleteFleetRuleCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FleetRuleOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new FleetRuleOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new FleetRuleOperationResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        var rule = await fleetRepository.GetRuleByIdAsync(request.RuleId, cancellationToken);
        if (rule is null || rule.FleetId != fleet.Id)
        {
            return new FleetRuleOperationResponse { Success = false, Message = "Rule not found." };
        }

        if (fleet.RequireApprovalForEdits)
        {
            var proposalId = await fleetRulesProposalService.CreateProposalAsync(
                fleet.Id,
                userId,
                FleetRuleProposalAction.Delete,
                CrewRuleChangeDescriber.DeleteTitle,
                CrewRuleChangeDescriber.BuildDeleteDescription(
                    rule.Title ?? string.Empty,
                    rule.Description ?? string.Empty),
                rule.Id,
                rule.Title ?? string.Empty,
                rule.Description ?? string.Empty,
                rule.IsPublic,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new FleetRuleOperationResponse
            {
                Success = true,
                Message = "Proposal submitted for fleet approval.",
                ProposalsSubmitted = true,
                ProposalId = proposalId
            };
        }

        rule.IsDeleted = true;
        rule.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new FleetRuleOperationResponse
        {
            Success = true,
            Message = "Rule deleted."
        };
    }
}
