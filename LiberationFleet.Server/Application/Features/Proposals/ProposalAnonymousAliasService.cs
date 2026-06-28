using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Proposals;

public class ProposalAnonymousAliasService(IProposalRepository proposalRepository)
{
    public async Task<ProposalAnonymousAlias> GetOrCreateAsync(
        int proposalId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var existing = await proposalRepository.GetAnonymousAliasAsync(proposalId, userId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var taken = await proposalRepository.GetAnonymousNicknamesAsync(proposalId, cancellationToken);
        var alias = new ProposalAnonymousAlias
        {
            ProposalId = proposalId,
            UserId = userId,
            Nickname = ProposalNicknameGenerator.Generate(taken),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await proposalRepository.AddAnonymousAliasAsync(alias, cancellationToken);
        return alias;
    }

    public async Task<ProposalAnonymousAlias?> RerollAsync(
        int proposalId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var alias = await proposalRepository.GetAnonymousAliasAsync(proposalId, userId, cancellationToken);
        if (alias is null)
        {
            return null;
        }

        var taken = await proposalRepository.GetAnonymousNicknamesAsync(proposalId, cancellationToken);
        var withoutCurrent = taken.Where(n => !string.Equals(n, alias.Nickname, StringComparison.OrdinalIgnoreCase)).ToList();
        alias.Nickname = ProposalNicknameGenerator.Generate(withoutCurrent);
        alias.UpdatedAt = DateTime.UtcNow;
        return alias;
    }

    public async Task<IReadOnlyDictionary<int, string>> GetNicknameMapAsync(
        int proposalId,
        IEnumerable<int> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        var aliases = await proposalRepository.GetAnonymousAliasesAsync(proposalId, ids, cancellationToken);
        return aliases.ToDictionary(a => a.UserId, a => a.Nickname, comparer: EqualityComparer<int>.Default);
    }
}
