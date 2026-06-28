using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Rules.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Rules.Commands.DeleteCrewRule;

public record DeleteCrewRuleCommand(
    int RuleId,
    string PlaintextTitle,
    string PlaintextDescription) : IRequest<RuleOperationResponse>;

public class DeleteCrewRuleCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IRuleRepository ruleRepository,
    CrewRulesProposalService crewRulesProposalService,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteCrewRuleCommand, RuleOperationResponse>
{
    public async Task<RuleOperationResponse> Handle(DeleteCrewRuleCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new RuleOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var rule = await ruleRepository.GetByIdAsync(request.RuleId, cancellationToken);
        if (rule is null)
        {
            return new RuleOperationResponse { Success = false, Message = "Rule not found." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, rule.CrewId, cancellationToken))
        {
            return new RuleOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        var crew = await crewRepository.GetByIdAsync(rule.CrewId, cancellationToken);
        if (crew is null)
        {
            return new RuleOperationResponse { Success = false, Message = "Crew not found." };
        }

        if (crew.RequireApprovalForEdits)
        {
            var proposalId = await crewRulesProposalService.CreateProposalAsync(
                crew.Id,
                userId,
                CrewRuleProposalAction.Delete,
                CrewRuleChangeDescriber.DeleteTitle,
                CrewRuleChangeDescriber.BuildDeleteDescription(request.PlaintextTitle, request.PlaintextDescription),
                rule.Id,
                nonce: null,
                ciphertext: null,
                keyVersion: 1,
                isPublic: rule.IsPublic,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new RuleOperationResponse
            {
                Success = true,
                Message = "Proposal submitted for crew approval.",
                ProposalsSubmitted = true,
                ProposalId = proposalId
            };
        }

        rule.IsDeleted = true;
        rule.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyCrewAsync(
            rule.CrewId,
            NotificationKind.RuleDeleted,
            "Rule deleted",
            "A crew rule was deleted.",
            "/app/crew/rules",
            relatedEntityId: rule.Id,
            excludeUserId: userId,
            cancellationToken: cancellationToken);

        return new RuleOperationResponse
        {
            Success = true,
            Message = "Rule deleted."
        };
    }
}
