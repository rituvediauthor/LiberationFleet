using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Application.Features.Rules;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Commands.UpdateFleetRule;

public record UpdateFleetRuleCommand(
    int RuleId,
    bool IsPublic,
    string Title,
    string Description) : IRequest<FleetRuleOperationResponse>;

public class UpdateFleetRuleCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    FleetRulesProposalService fleetRulesProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateFleetRuleCommand, FleetRuleOperationResponse>
{
    public async Task<FleetRuleOperationResponse> Handle(UpdateFleetRuleCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FleetRuleOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return new FleetRuleOperationResponse { Success = false, Message = "Rule title is required." };
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

        var title = request.Title.Trim();
        var description = request.Description.Trim();

        if (fleet.RequireApprovalForEdits)
        {
            var proposalId = await fleetRulesProposalService.CreateProposalAsync(
                fleet.Id,
                userId,
                FleetRuleProposalAction.Update,
                CrewRuleChangeDescriber.UpdateTitle,
                CrewRuleChangeDescriber.BuildUpdateDescription(
                    rule.Title ?? string.Empty,
                    rule.Description ?? string.Empty,
                    title,
                    description),
                rule.Id,
                title,
                description,
                request.IsPublic,
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

        rule.UpdatedAt = DateTime.UtcNow;
        rule.IsPublic = request.IsPublic;
        rule.Title = title;
        rule.Description = description;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new FleetRuleOperationResponse
        {
            Success = true,
            Message = "Rule updated.",
            RuleId = rule.Id
        };
    }
}
