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
        _context.Crews.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Crew?> GetByJoinCodeAsync(string joinCode, CancellationToken cancellationToken = default) =>
        _context.Crews.FirstOrDefaultAsync(c => c.JoinCode == joinCode, cancellationToken);

    public async Task AddAsync(Crew crew, CancellationToken cancellationToken = default)
    {
        await _context.Crews.AddAsync(crew, cancellationToken);
    }

    public async Task<IReadOnlyList<Crew>> SearchPublicAsync(CrewScope scope, CancellationToken cancellationToken = default)
    {
        return await _context.Crews
            .Where(c => c.Privacy == CrewPrivacy.Public && c.Scope == scope)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountMembersAsync(int crewId, CancellationToken cancellationToken = default) =>
        _context.CrewMemberships.CountAsync(m => m.CrewId == crewId && !m.IsBanned, cancellationToken);
}
