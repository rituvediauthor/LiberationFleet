using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class CrewMembershipRepository : ICrewMembershipRepository
{
    private readonly ApplicationDbContext _context;

    public CrewMembershipRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<CrewMembership?> GetActiveMembershipAsync(int userId, CancellationToken cancellationToken = default) =>
        _context.CrewMemberships
            .Include(m => m.Crew)
            .OrderByDescending(m => m.JoinedAt)
            .FirstOrDefaultAsync(
                m => m.UserId == userId && !m.IsBanned && m.LeftAt == null,
                cancellationToken);

    public Task<bool> IsUserBannedFromCrewAsync(int userId, int crewId, CancellationToken cancellationToken = default) =>
        _context.CrewMemberships.AnyAsync(
            m => m.UserId == userId && m.CrewId == crewId && m.IsBanned,
            cancellationToken);

    public Task<bool> IsUserInCrewAsync(int userId, int crewId, CancellationToken cancellationToken = default) =>
        _context.CrewMemberships.AnyAsync(
            m => m.UserId == userId && m.CrewId == crewId && !m.IsBanned && m.LeftAt == null,
            cancellationToken);

    public async Task<IReadOnlyList<CrewMembership>> GetActiveMembersByCrewIdAsync(int crewId, CancellationToken cancellationToken = default) =>
        await _context.CrewMemberships
            .AsNoTracking()
            .AsSplitQuery()
            .Include(m => m.Crew)
            .Include(m => m.User)
                .ThenInclude(u => u.PaymentPlatforms)
                    .ThenInclude(p => p.CrewPaymentPlatform)
            .Where(m => m.CrewId == crewId && !m.IsBanned && m.LeftAt == null && !m.User.IsCrewGiftRecipient)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CrewMembership>> GetBannedMembersByCrewIdAsync(
        int crewId,
        CancellationToken cancellationToken = default) =>
        await _context.CrewMemberships
            .AsNoTracking()
            .Include(m => m.User)
            .Where(m => m.CrewId == crewId && m.IsBanned)
            .OrderBy(m => m.User.Username)
            .ToListAsync(cancellationToken);

    public Task<CrewMembership?> GetMembershipAsync(int userId, int crewId, CancellationToken cancellationToken = default) =>
        _context.CrewMemberships.FirstOrDefaultAsync(
            m => m.UserId == userId && m.CrewId == crewId,
            cancellationToken);

    public async Task<CrewMembership> ReactivateOrCreateAsync(
        int userId,
        int crewId,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetMembershipAsync(userId, crewId, cancellationToken);
        if (existing is not null)
        {
            existing.IsBanned = false;
            existing.LeftAt = null;
            // Returning members keep prior crew-specific stats/roles; they must re-enter season.
            existing.IsInSeason = false;
            existing.IsSeasonReady = false;
            return existing;
        }

        var membership = new CrewMembership
        {
            UserId = userId,
            CrewId = crewId,
            JoinedAt = DateTime.UtcNow
        };
        await AddAsync(membership, cancellationToken);
        return membership;
    }

    public async Task AddAsync(CrewMembership membership, CancellationToken cancellationToken = default)
    {
        await _context.CrewMemberships.AddAsync(membership, cancellationToken);
    }

    public void Remove(CrewMembership membership) =>
        _context.CrewMemberships.Remove(membership);

    public void MarkLeft(CrewMembership membership, DateTime leftAtUtc)
    {
        membership.LeftAt = leftAtUtc;
        membership.IsInSeason = false;
        membership.IsSeasonReady = false;
    }

    public async Task<IReadOnlySet<int>> GetActiveCrewIdsForUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var ids = await _context.CrewMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId && !m.IsBanned && m.LeftAt == null)
            .Select(m => m.CrewId)
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    public async Task<IReadOnlySet<int>> GetUserIdsWithActiveMembershipAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new HashSet<int>();
        }

        var active = await _context.CrewMemberships
            .AsNoTracking()
            .Where(m => ids.Contains(m.UserId) && !m.IsBanned && m.LeftAt == null)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return active.ToHashSet();
    }

    public async Task<IReadOnlySet<int>> GetBannedCrewIdsForUserAsync(
        int userId,
        IReadOnlyCollection<int> crewIds,
        CancellationToken cancellationToken = default)
    {
        var ids = crewIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new HashSet<int>();
        }

        var banned = await _context.CrewMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.IsBanned && ids.Contains(m.CrewId))
            .Select(m => m.CrewId)
            .ToListAsync(cancellationToken);
        return banned.ToHashSet();
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlySet<int>>> GetActiveNonPlaceholderUserIdsByCrewIdsAsync(
        IReadOnlyCollection<int> crewIds,
        CancellationToken cancellationToken = default)
    {
        var ids = crewIds.Distinct().ToList();
        var result = ids.ToDictionary(id => id, _ => (IReadOnlySet<int>)new HashSet<int>());
        if (ids.Count == 0)
        {
            return result;
        }

        var rows = await _context.CrewMemberships
            .AsNoTracking()
            .Where(m => ids.Contains(m.CrewId)
                && !m.IsBanned
                && m.LeftAt == null
                && !m.IsPlaceholderMember
                && !m.User.IsCrewGiftRecipient)
            .Select(m => new { m.CrewId, m.UserId })
            .ToListAsync(cancellationToken);

        var sets = ids.ToDictionary(id => id, _ => new HashSet<int>());
        foreach (var row in rows)
        {
            sets[row.CrewId].Add(row.UserId);
        }

        return sets.ToDictionary(pair => pair.Key, pair => (IReadOnlySet<int>)pair.Value);
    }
}
