using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class CrewPaymentPlatformRepository : ICrewPaymentPlatformRepository
{
    private readonly ApplicationDbContext _context;

    public CrewPaymentPlatformRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CrewPaymentPlatform>> GetUsedByOtherCrewmatesAsync(
        int crewId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var platformIds = await _context.UserPaymentPlatforms
            .Where(upp => upp.UserId != userId)
            .Where(upp => _context.CrewMemberships.Any(m =>
                m.UserId == upp.UserId && m.CrewId == crewId && !m.IsBanned))
            .Select(upp => upp.CrewPaymentPlatformId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return await _context.CrewPaymentPlatforms
            .Where(p => p.CrewId == crewId && platformIds.Contains(p.Id))
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CrewPaymentPlatform>> GetByCrewIdAsync(int crewId, CancellationToken cancellationToken = default) =>
        await _context.CrewPaymentPlatforms
            .Where(p => p.CrewId == crewId)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public Task<CrewPaymentPlatform?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.CrewPaymentPlatforms.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<CrewPaymentPlatform?> GetByCrewAndNameAsync(int crewId, string name, CancellationToken cancellationToken = default) =>
        _context.CrewPaymentPlatforms.FirstOrDefaultAsync(
            p => p.CrewId == crewId && p.Name == name,
            cancellationToken);

    public async Task<CrewPaymentPlatform> AddAsync(CrewPaymentPlatform platform, CancellationToken cancellationToken = default)
    {
        await _context.CrewPaymentPlatforms.AddAsync(platform, cancellationToken);
        return platform;
    }

    public Task<bool> ExistsForCrewAsync(int crewId, int platformId, CancellationToken cancellationToken = default) =>
        _context.CrewPaymentPlatforms.AnyAsync(p => p.CrewId == crewId && p.Id == platformId, cancellationToken);
}
