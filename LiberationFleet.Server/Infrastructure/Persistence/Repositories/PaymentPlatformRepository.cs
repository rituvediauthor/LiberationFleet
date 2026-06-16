using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class PaymentPlatformRepository : IPaymentPlatformRepository
{
    private readonly ApplicationDbContext _context;

    public PaymentPlatformRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PaymentPlatform>> GetAllOrderedAsync(CancellationToken cancellationToken = default) =>
        await _context.PaymentPlatforms
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) =>
        _context.PaymentPlatforms.AnyAsync(p => p.Id == id, cancellationToken);
}
