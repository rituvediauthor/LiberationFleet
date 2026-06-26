using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class RuleRepository : IRuleRepository
{
    private readonly ApplicationDbContext _context;

    public RuleRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<CrewRule?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.CrewRules.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);

    public Task<CrewRule?> GetByIdWithAuthorAsync(int id, CancellationToken cancellationToken = default) =>
        _context.CrewRules
            .Include(r => r.CreatedByUser)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<CrewRule>> GetByCrewIdAsync(int crewId, CancellationToken cancellationToken = default) =>
        await _context.CrewRules
            .Include(r => r.CreatedByUser)
            .Where(r => r.CrewId == crewId && !r.IsDeleted)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(CrewRule rule, CancellationToken cancellationToken = default) =>
        await _context.CrewRules.AddAsync(rule, cancellationToken);
}
