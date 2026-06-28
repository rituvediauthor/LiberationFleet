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
            CreatedByUsername = rule.IsPublic || envelope is null ? rule.CreatedByUser.Username : string.Empty,
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt,
            IsPublic = rule.IsPublic,
            Title = rule.IsPublic ? rule.Title : null,
            Description = rule.IsPublic ? rule.Description : null,
            HasEncryptedContent = !rule.IsPublic && envelope is not null,
            EncryptedPayload = !rule.IsPublic && envelope is not null ? CryptoMapper.MapPayload(envelope) : null
        };

    public static RuleDetailDto MapDetail(CrewRule rule, EncryptedContentEnvelope? envelope) =>
        new()
        {
            Id = rule.Id,
            CreatedByUserId = rule.CreatedByUserId,
            CreatedByUsername = rule.IsPublic || envelope is null ? rule.CreatedByUser.Username : string.Empty,
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt,
            IsPublic = rule.IsPublic,
            Title = rule.IsPublic ? rule.Title : null,
            Description = rule.IsPublic ? rule.Description : null,
            HasEncryptedContent = !rule.IsPublic && envelope is not null,
            EncryptedPayload = !rule.IsPublic && envelope is not null ? CryptoMapper.MapPayload(envelope) : null
        };
}
