using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Rules;
using LiberationFleet.Server.Application.Features.Rules.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Rules.Queries.GetCrewRule;

public record GetCrewRuleQuery(int RuleId) : IRequest<RuleDetailResponse>;

public class GetCrewRuleQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IRuleRepository ruleRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetCrewRuleQuery, RuleDetailResponse>
{
    public async Task<RuleDetailResponse> Handle(GetCrewRuleQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new RuleDetailResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new RuleDetailResponse { Success = false, Message = "You are not in a crew." };
        }

        var rule = await ruleRepository.GetByIdWithAuthorAsync(request.RuleId, cancellationToken);
        if (rule is null || rule.CrewId != membership.CrewId)
        {
            return new RuleDetailResponse { Success = false, Message = "Rule not found." };
        }

        var envelope = await cryptoRepository.GetEnvelopeAsync(
            EncryptedContentType.RulesDocument,
            rule.Id.ToString(),
            cancellationToken);

        return new RuleDetailResponse
        {
            Success = true,
            Message = "Rule loaded.",
            Rule = RuleMapper.MapDetail(rule, envelope)
        };
    }
}
