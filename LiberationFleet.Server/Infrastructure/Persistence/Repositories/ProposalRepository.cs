using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class ProposalRepository : IProposalRepository
{
    private readonly ApplicationDbContext _context;

    public ProposalRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Proposal?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Proposals.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

    public Task<Proposal?> GetByIdWithAuthorAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Proposals
            .Include(p => p.AuthorUser)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<Proposal>> GetByCrewAndStatusAsync(
        int crewId,
        ProposalStatus status,
        CancellationToken cancellationToken = default) =>
        await _context.Proposals
            .Include(p => p.AuthorUser)
            .Where(p => p.CrewId == crewId && !p.IsDeleted && p.Status == status)
            .OrderByDescending(p => p.LastActivityAt)
            .ToListAsync(cancellationToken);

    public Task<int> GetActiveCrewMemberCountAsync(int crewId, CancellationToken cancellationToken = default) =>
        _context.CrewMemberships
            .CountAsync(m => m.CrewId == crewId && !m.IsBanned, cancellationToken);

    public Task<ProposalVote?> GetVoteAsync(int proposalId, int userId, CancellationToken cancellationToken = default) =>
        _context.ProposalVotes.FirstOrDefaultAsync(v => v.ProposalId == proposalId && v.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<ProposalComment>> GetCommentsByProposalIdAsync(
        int proposalId,
        CancellationToken cancellationToken = default) =>
        await _context.ProposalComments
            .Include(c => c.AuthorUser)
            .Where(c => c.ProposalId == proposalId && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<ProposalComment?> GetCommentByIdAsync(int commentId, CancellationToken cancellationToken = default) =>
        _context.ProposalComments
            .Include(c => c.AuthorUser)
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted, cancellationToken);

    public async Task AddProposalAsync(Proposal proposal, CancellationToken cancellationToken = default) =>
        await _context.Proposals.AddAsync(proposal, cancellationToken);

    public async Task AddVoteAsync(ProposalVote vote, CancellationToken cancellationToken = default) =>
        await _context.ProposalVotes.AddAsync(vote, cancellationToken);

    public async Task AddCommentAsync(ProposalComment comment, CancellationToken cancellationToken = default) =>
        await _context.ProposalComments.AddAsync(comment, cancellationToken);

    public void RemoveVote(ProposalVote vote) => _context.ProposalVotes.Remove(vote);

    public Task<ProposalCrewSettingChange?> GetCrewSettingChangeByProposalIdAsync(
        int proposalId,
        CancellationToken cancellationToken = default) =>
        _context.ProposalCrewSettingChanges
            .FirstOrDefaultAsync(c => c.ProposalId == proposalId, cancellationToken);

    public async Task<IReadOnlyDictionary<int, ProposalCrewSettingChange>> GetCrewSettingChangesByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default)
    {
        var ids = proposalIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, ProposalCrewSettingChange>();
        }

        var changes = await _context.ProposalCrewSettingChanges
            .Where(c => ids.Contains(c.ProposalId))
            .ToListAsync(cancellationToken);

        return changes.ToDictionary(c => c.ProposalId);
    }

    public async Task AddCrewSettingChangeAsync(
        ProposalCrewSettingChange change,
        CancellationToken cancellationToken = default) =>
        await _context.ProposalCrewSettingChanges.AddAsync(change, cancellationToken);

    public Task<ProposalCrewRuleChange?> GetCrewRuleChangeByProposalIdAsync(
        int proposalId,
        CancellationToken cancellationToken = default) =>
        _context.ProposalCrewRuleChanges
            .FirstOrDefaultAsync(c => c.ProposalId == proposalId, cancellationToken);

    public async Task<IReadOnlyDictionary<int, ProposalCrewRuleChange>> GetCrewRuleChangesByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default)
    {
        var ids = proposalIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, ProposalCrewRuleChange>();
        }

        var changes = await _context.ProposalCrewRuleChanges
            .Where(c => ids.Contains(c.ProposalId))
            .ToListAsync(cancellationToken);

        return changes.ToDictionary(c => c.ProposalId);
    }

    public async Task AddCrewRuleChangeAsync(
        ProposalCrewRuleChange change,
        CancellationToken cancellationToken = default) =>
        await _context.ProposalCrewRuleChanges.AddAsync(change, cancellationToken);
}
