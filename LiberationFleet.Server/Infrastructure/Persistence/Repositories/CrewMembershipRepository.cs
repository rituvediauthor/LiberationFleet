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
            .FirstOrDefaultAsync(m => m.UserId == userId && !m.IsBanned, cancellationToken);

    public Task<bool> IsUserBannedFromCrewAsync(int userId, int crewId, CancellationToken cancellationToken = default) =>
        _context.CrewMemberships.AnyAsync(
            m => m.UserId == userId && m.CrewId == crewId && m.IsBanned,
            cancellationToken);

    public Task<bool> IsUserInCrewAsync(int userId, int crewId, CancellationToken cancellationToken = default) =>
        _context.CrewMemberships.AnyAsync(
            m => m.UserId == userId && m.CrewId == crewId && !m.IsBanned,
            cancellationToken);

    public async Task<IReadOnlyList<CrewMembership>> GetActiveMembersByCrewIdAsync(int crewId, CancellationToken cancellationToken = default) =>
        await _context.CrewMemberships
            .Include(m => m.User)
                .ThenInclude(u => u.PaymentPlatforms)
            .Where(m => m.CrewId == crewId && !m.IsBanned)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(CrewMembership membership, CancellationToken cancellationToken = default)
    {
        await _context.CrewMemberships.AddAsync(membership, cancellationToken);
    }
}
