using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Rules.Contracts;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Rules;

public static class RuleMapper
{
    public static RuleListItemDto MapListItem(CrewRule rule, EncryptedContentEnvelope? envelope) =>
        new()
        {
            Id = rule.Id,
            CreatedByUserId = rule.CreatedByUserId,
            CreatedByUsername = envelope is null ? rule.CreatedByUser.Username : string.Empty,
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt,
            HasEncryptedContent = envelope is not null,
            EncryptedPayload = envelope is not null ? CryptoMapper.MapPayload(envelope) : null
        };

    public static RuleDetailDto MapDetail(CrewRule rule, EncryptedContentEnvelope? envelope) =>
        new()
        {
            Id = rule.Id,
            CreatedByUserId = rule.CreatedByUserId,
            CreatedByUsername = envelope is null ? rule.CreatedByUser.Username : string.Empty,
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt,
            HasEncryptedContent = envelope is not null,
            EncryptedPayload = envelope is not null ? CryptoMapper.MapPayload(envelope) : null
        };
}
