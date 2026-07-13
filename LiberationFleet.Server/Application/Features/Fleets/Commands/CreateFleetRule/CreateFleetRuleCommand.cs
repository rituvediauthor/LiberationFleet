using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Application.Features.Rules;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Commands.CreateFleetRule;

public record CreateFleetRuleCommand(
    bool IsPublic,
    string Title,
    string Description) : IRequest<FleetRuleOperationResponse>;

public class CreateFleetRuleCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    FleetRulesProposalService fleetRulesProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateFleetRuleCommand, FleetRuleOperationResponse>
{
    public async Task<FleetRuleOperationResponse> Handle(CreateFleetRuleCommand request, CancellationToken cancellationToken)
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

        var title = request.Title.Trim();
        var description = request.Description.Trim();

        if (fleet.RequireApprovalForEdits)
        {
            var proposalId = await fleetRulesProposalService.CreateProposalAsync(
                fleet.Id,
                userId,
                FleetRuleProposalAction.Create,
                CrewRuleChangeDescriber.CreateTitle,
                CrewRuleChangeDescriber.BuildCreateDescription(title, description),
                ruleId: null,
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

        var utcNow = DateTime.UtcNow;
        var rule = new FleetRule
        {
            FleetId = fleet.Id,
            CreatedByUserId = userId,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            IsPublic = request.IsPublic,
            Title = title,
            Description = description
        };

        await fleetRepository.AddRuleAsync(rule, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new FleetRuleOperationResponse
        {
            Success = true,
            Message = "Rule created.",
            RuleId = rule.Id
        };
    }
}
