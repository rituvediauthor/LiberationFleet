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

    public Task<ProposalCrewChatChange?> GetCrewChatChangeByProposalIdAsync(
        int proposalId,
        CancellationToken cancellationToken = default) =>
        _context.ProposalCrewChatChanges
            .FirstOrDefaultAsync(c => c.ProposalId == proposalId, cancellationToken);

    public async Task<IReadOnlyDictionary<int, ProposalCrewChatChange>> GetCrewChatChangesByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default)
    {
        var ids = proposalIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, ProposalCrewChatChange>();
        }

        var changes = await _context.ProposalCrewChatChanges
            .Where(c => ids.Contains(c.ProposalId))
            .ToListAsync(cancellationToken);

        return changes.ToDictionary(c => c.ProposalId);
    }

    public async Task AddCrewChatChangeAsync(
        ProposalCrewChatChange change,
        CancellationToken cancellationToken = default) =>
        await _context.ProposalCrewChatChanges.AddAsync(change, cancellationToken);

    public Task<ProposalCrewmateKick?> GetCrewmateKickByProposalIdAsync(
        int proposalId,
        CancellationToken cancellationToken = default) =>
        _context.ProposalCrewmateKicks
            .FirstOrDefaultAsync(k => k.ProposalId == proposalId, cancellationToken);

    public async Task<IReadOnlyDictionary<int, ProposalCrewmateKick>> GetCrewmateKicksByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default)
    {
        var ids = proposalIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, ProposalCrewmateKick>();
        }

        var kicks = await _context.ProposalCrewmateKicks
            .Where(k => ids.Contains(k.ProposalId))
            .ToListAsync(cancellationToken);

        return kicks.ToDictionary(k => k.ProposalId);
    }

    public async Task AddCrewmateKickAsync(
        ProposalCrewmateKick kick,
        CancellationToken cancellationToken = default) =>
        await _context.ProposalCrewmateKicks.AddAsync(kick, cancellationToken);

    public Task<ProposalCrewmateKick?> GetPendingCrewmateKickForTargetAsync(
        int crewId,
        int targetUserId,
        CancellationToken cancellationToken = default) =>
        _context.ProposalCrewmateKicks
            .Include(k => k.Proposal)
            .Where(k =>
                k.TargetUserId == targetUserId
                && k.Proposal.CrewId == crewId
                && !k.Proposal.IsDeleted
                && k.Proposal.Status == ProposalStatus.Pending
                && k.Proposal.Kind == ProposalKind.CrewmateKick)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ProposalCrewmateRejoin?> GetCrewmateRejoinByProposalIdAsync(
        int proposalId,
        CancellationToken cancellationToken = default) =>
        _context.ProposalCrewmateRejoins
            .FirstOrDefaultAsync(r => r.ProposalId == proposalId, cancellationToken);

    public async Task<IReadOnlyDictionary<int, ProposalCrewmateRejoin>> GetCrewmateRejoinsByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default)
    {
        var ids = proposalIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, ProposalCrewmateRejoin>();
        }

        var rejoins = await _context.ProposalCrewmateRejoins
            .Where(r => ids.Contains(r.ProposalId))
            .ToListAsync(cancellationToken);

        return rejoins.ToDictionary(r => r.ProposalId);
    }

    public async Task AddCrewmateRejoinAsync(
        ProposalCrewmateRejoin rejoin,
        CancellationToken cancellationToken = default) =>
        await _context.ProposalCrewmateRejoins.AddAsync(rejoin, cancellationToken);

    public Task<ProposalCrewmateRejoin?> GetPendingCrewmateRejoinForTargetAsync(
        int crewId,
        int targetUserId,
        CancellationToken cancellationToken = default) =>
        _context.ProposalCrewmateRejoins
            .Include(r => r.Proposal)
            .Where(r =>
                r.TargetUserId == targetUserId
                && r.Proposal.CrewId == crewId
                && !r.Proposal.IsDeleted
                && r.Proposal.Status == ProposalStatus.Pending
                && r.Proposal.Kind == ProposalKind.CrewmateRejoin)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ProposalCrewJoinRequest?> GetCrewJoinRequestByProposalIdAsync(
        int proposalId,
        CancellationToken cancellationToken = default) =>
        _context.ProposalCrewJoinRequests
            .FirstOrDefaultAsync(j => j.ProposalId == proposalId, cancellationToken);

    public async Task<IReadOnlyDictionary<int, ProposalCrewJoinRequest>> GetCrewJoinRequestsByProposalIdsAsync(
        IEnumerable<int> proposalIds,
        CancellationToken cancellationToken = default)
    {
        var ids = proposalIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, ProposalCrewJoinRequest>();
        }

        var requests = await _context.ProposalCrewJoinRequests
            .Where(j => ids.Contains(j.ProposalId))
            .ToListAsync(cancellationToken);

        return requests.ToDictionary(j => j.ProposalId);
    }

    public async Task AddCrewJoinRequestAsync(
        ProposalCrewJoinRequest joinRequest,
        CancellationToken cancellationToken = default) =>
        await _context.ProposalCrewJoinRequests.AddAsync(joinRequest, cancellationToken);

    public Task<ProposalCrewJoinRequest?> GetPendingJoinRequestForApplicantAndCrewAsync(
        int applicantUserId,
        int crewId,
        CancellationToken cancellationToken = default) =>
        _context.ProposalCrewJoinRequests
            .Include(j => j.Proposal)
            .Where(j =>
                j.ApplicantUserId == applicantUserId
                && j.Proposal.CrewId == crewId
                && !j.Proposal.IsDeleted
                && j.Proposal.Status == ProposalStatus.Pending
                && j.Proposal.Kind == ProposalKind.CrewJoinRequest)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Proposal>> GetJoinRequestProposalsByApplicantAsync(
        int applicantUserId,
        CancellationToken cancellationToken = default) =>
        await _context.Proposals
            .Where(p =>
                !p.IsDeleted
                && p.Kind == ProposalKind.CrewJoinRequest
                && p.AuthorUserId == applicantUserId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<int>> GetPendingJoinApplicantUserIdsForCrewAsync(
        int crewId,
        CancellationToken cancellationToken = default) =>
        await _context.ProposalCrewJoinRequests
            .Include(j => j.Proposal)
            .Where(j =>
                j.Proposal.CrewId == crewId
                && !j.Proposal.IsDeleted
                && j.Proposal.Status == ProposalStatus.Pending
                && j.Proposal.Kind == ProposalKind.CrewJoinRequest
                && !j.IsKeyPrepared)
            .Select(j => j.ApplicantUserId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task RejectPendingJoinRequestsForApplicantAsync(
        int applicantUserId,
        int exceptProposalId,
        CancellationToken cancellationToken = default)
    {
        var pending = await _context.Proposals
            .Where(p =>
                !p.IsDeleted
                && p.AuthorUserId == applicantUserId
                && p.Kind == ProposalKind.CrewJoinRequest
                && p.Status == ProposalStatus.Pending
                && p.Id != exceptProposalId)
            .ToListAsync(cancellationToken);

        foreach (var proposal in pending)
        {
            proposal.Status = ProposalStatus.Rejected;
            proposal.LastActivityAt = DateTime.UtcNow;
        }
    }

    public Task<ProposalAnonymousAlias?> GetAnonymousAliasAsync(
        int proposalId,
        int userId,
        CancellationToken cancellationToken = default) =>
        _context.ProposalAnonymousAliases
            .FirstOrDefaultAsync(a => a.ProposalId == proposalId && a.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<ProposalAnonymousAlias>> GetAnonymousAliasesAsync(
        int proposalId,
        IEnumerable<int> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return Array.Empty<ProposalAnonymousAlias>();
        }

        return await _context.ProposalAnonymousAliases
            .Where(a => a.ProposalId == proposalId && ids.Contains(a.UserId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetAnonymousNicknamesAsync(
        int proposalId,
        CancellationToken cancellationToken = default) =>
        await _context.ProposalAnonymousAliases
            .Where(a => a.ProposalId == proposalId)
            .Select(a => a.Nickname)
            .ToListAsync(cancellationToken);

    public async Task AddAnonymousAliasAsync(
        ProposalAnonymousAlias alias,
        CancellationToken cancellationToken = default) =>
        await _context.ProposalAnonymousAliases.AddAsync(alias, cancellationToken);
}
