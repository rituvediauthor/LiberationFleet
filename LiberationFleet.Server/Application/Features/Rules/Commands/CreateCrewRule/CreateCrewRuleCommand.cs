using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Rules.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Rules.Commands.CreateCrewRule;

public record CreateCrewRuleCommand(
    string Nonce,
    string Ciphertext,
    int KeyVersion,
    string PlaintextTitle,
    string PlaintextDescription) : IRequest<RuleOperationResponse>;

public class CreateCrewRuleCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IRuleRepository ruleRepository,
    ICryptoRepository cryptoRepository,
    CrewRulesProposalService crewRulesProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateCrewRuleCommand, RuleOperationResponse>
{
    public async Task<RuleOperationResponse> Handle(CreateCrewRuleCommand request, CancellationToken cancellationToken)
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
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new RuleOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var crew = await crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
        if (crew is null)
        {
            return new RuleOperationResponse { Success = false, Message = "Crew not found." };
        }

        if (crew.RequireApprovalForEdits)
        {
            var proposalId = await crewRulesProposalService.CreateProposalAsync(
                crew.Id,
                userId,
                CrewRuleProposalAction.Create,
                CrewRuleChangeDescriber.CreateTitle,
                CrewRuleChangeDescriber.BuildCreateDescription(request.PlaintextTitle, request.PlaintextDescription),
                ruleId: null,
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
        var rule = new CrewRule
        {
            CrewId = membership.CrewId,
            CreatedByUserId = userId,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        await ruleRepository.AddAsync(rule, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.RulesDocument,
            ResourceId = rule.Id.ToString(),
            CrewId = membership.CrewId,
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
            Message = "Rule created.",
            RuleId = rule.Id
        };
    }
}
