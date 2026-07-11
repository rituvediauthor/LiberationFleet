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
                    .ThenInclude(p => p.CrewPaymentPlatform)
            .Where(m => m.CrewId == crewId && !m.IsBanned && !m.User.IsCrewGiftRecipient)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CrewMembership>> GetBannedMembersByCrewIdAsync(
        int crewId,
        CancellationToken cancellationToken = default) =>
        await _context.CrewMemberships
            .Include(m => m.User)
            .Where(m => m.CrewId == crewId && m.IsBanned)
            .OrderBy(m => m.User.Username)
            .ToListAsync(cancellationToken);

    public Task<CrewMembership?> GetMembershipAsync(int userId, int crewId, CancellationToken cancellationToken = default) =>
        _context.CrewMemberships.FirstOrDefaultAsync(
            m => m.UserId == userId && m.CrewId == crewId,
            cancellationToken);

    public async Task AddAsync(CrewMembership membership, CancellationToken cancellationToken = default)
    {
        await _context.CrewMemberships.AddAsync(membership, cancellationToken);
    }

    public void Remove(CrewMembership membership) =>
        _context.CrewMemberships.Remove(membership);
}
