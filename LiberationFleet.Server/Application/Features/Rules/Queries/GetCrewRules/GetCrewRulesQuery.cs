using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Rules;
using LiberationFleet.Server.Application.Features.Rules.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Rules.Queries.GetCrewRules;

public record GetCrewRulesQuery() : IRequest<RuleListResponse>;

public class GetCrewRulesQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IRuleRepository ruleRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetCrewRulesQuery, RuleListResponse>
{
    public async Task<RuleListResponse> Handle(GetCrewRulesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new RuleListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new RuleListResponse { Success = false, Message = "You are not in a crew." };
        }

        var rules = await ruleRepository.GetByCrewIdAsync(membership.CrewId, cancellationToken);
        var resourceIds = rules.Select(r => r.Id.ToString()).ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.RulesDocument,
            resourceIds,
            membership.CrewId,
            cancellationToken);
        var envelopeById = envelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var items = rules.Select(rule =>
        {
            envelopeById.TryGetValue(rule.Id.ToString(), out var envelope);
            return RuleMapper.MapListItem(rule, envelope);
        }).ToList();

        return new RuleListResponse
        {
            Success = true,
            Message = "Rules loaded.",
            Items = items
        };
    }
}
