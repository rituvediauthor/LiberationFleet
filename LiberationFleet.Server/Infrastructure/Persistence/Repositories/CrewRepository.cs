using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class CrewRepository : ICrewRepository
{
    private readonly ApplicationDbContext _context;

    public CrewRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Crew?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Crews
            .Include(c => c.AllowedZipCodes)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Crew?> GetByJoinCodeAsync(string joinCode, CancellationToken cancellationToken = default) =>
        _context.Crews
            .Include(c => c.AllowedZipCodes)
            .FirstOrDefaultAsync(c => c.JoinCode == joinCode, cancellationToken);

    public async Task AddAsync(Crew crew, CancellationToken cancellationToken = default)
    {
        await _context.Crews.AddAsync(crew, cancellationToken);
    }

    public async Task<IReadOnlyList<Crew>> SearchPublicAsync(CrewScope scope, CancellationToken cancellationToken = default)
    {
        return await _context.Crews
            .Include(c => c.AllowedZipCodes)
            .Where(c => c.Privacy == CrewPrivacy.Public && c.Scope == scope)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountMembersAsync(int crewId, CancellationToken cancellationToken = default) =>
        _context.CrewMemberships.CountAsync(
            m => m.CrewId == crewId && !m.IsBanned && m.LeftAt == null && !m.IsPlaceholderMember,
            cancellationToken);

    public async Task<IReadOnlyDictionary<int, int>> CountMembersByCrewIdsAsync(
        IReadOnlyCollection<int> crewIds,
        CancellationToken cancellationToken = default)
    {
        var ids = crewIds.Distinct().ToList();
        var counts = ids.ToDictionary(id => id, _ => 0);
        if (ids.Count == 0)
        {
            return counts;
        }

        var rows = await _context.CrewMemberships
            .AsNoTracking()
            .Where(m => ids.Contains(m.CrewId) && !m.IsBanned && m.LeftAt == null && !m.IsPlaceholderMember)
            .GroupBy(m => m.CrewId)
            .Select(g => new { CrewId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            counts[row.CrewId] = row.Count;
        }

        return counts;
    }
}
