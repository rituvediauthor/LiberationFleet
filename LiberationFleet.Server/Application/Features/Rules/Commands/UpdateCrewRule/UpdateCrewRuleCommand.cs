using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Rules.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Rules.Commands.UpdateCrewRule;

public record UpdateCrewRuleCommand(
    int RuleId,
    string Nonce,
    string Ciphertext,
    int KeyVersion,
    string PlaintextTitle,
    string PlaintextDescription,
    string PlaintextOldTitle,
    string PlaintextOldDescription) : IRequest<RuleOperationResponse>;

public class UpdateCrewRuleCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IRuleRepository ruleRepository,
    ICryptoRepository cryptoRepository,
    CrewRulesProposalService crewRulesProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateCrewRuleCommand, RuleOperationResponse>
{
    public async Task<RuleOperationResponse> Handle(UpdateCrewRuleCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new RuleOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new RuleOperationResponse { Success = false, Message = "Encrypted rule content is required." };
        }

        if (string.IsNullOrWhiteSpace(request.PlaintextTitle))
        {
            return new RuleOperationResponse { Success = false, Message = "Rule title is required." };
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
                CrewRuleProposalAction.Update,
                CrewRuleChangeDescriber.UpdateTitle,
                CrewRuleChangeDescriber.BuildUpdateDescription(
                    request.PlaintextOldTitle,
                    request.PlaintextOldDescription,
                    request.PlaintextTitle,
                    request.PlaintextDescription),
                rule.Id,
                request.Nonce.Trim(),
                request.Ciphertext.Trim(),
                request.KeyVersion,
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

        var utcNow = DateTime.UtcNow;
        rule.UpdatedAt = utcNow;

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.RulesDocument,
            ResourceId = rule.Id.ToString(),
            CrewId = rule.CrewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RuleOperationResponse
        {
            Success = true,
            Message = "Rule updated.",
            RuleId = rule.Id
        };
    }
}
