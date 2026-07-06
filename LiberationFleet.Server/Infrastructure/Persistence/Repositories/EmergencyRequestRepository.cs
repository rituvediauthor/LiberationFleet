using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class EmergencyRequestRepository : IEmergencyRequestRepository
{
    private readonly ApplicationDbContext _context;

    public EmergencyRequestRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<EmergencyRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.EmergencyRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<EmergencyRequest?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default) =>
        _context.EmergencyRequests
            .Include(r => r.RequesterUser)
                .ThenInclude(u => u.PaymentPlatforms)
                    .ThenInclude(p => p.CrewPaymentPlatform)
            .Include(r => r.SplitOffers)
            .Include(r => r.GiftResponses)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<EmergencyRequest>> GetOpenByCrewIdAsync(
        int crewId,
        CancellationToken cancellationToken = default) =>
        await _context.EmergencyRequests
            .Include(r => r.RequesterUser)
            .Where(r => r.CrewId == crewId && r.Status == EmergencyRequestStatus.Open)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(EmergencyRequest request, CancellationToken cancellationToken = default) =>
        await _context.EmergencyRequests.AddAsync(request, cancellationToken);

    public async Task AddSplitOfferAsync(EmergencySplitOffer offer, CancellationToken cancellationToken = default) =>
        await _context.EmergencySplitOffers.AddAsync(offer, cancellationToken);

    public async Task AddGiftResponseAsync(EmergencyGiftResponse response, CancellationToken cancellationToken = default) =>
        await _context.EmergencyGiftResponses.AddAsync(response, cancellationToken);
}
