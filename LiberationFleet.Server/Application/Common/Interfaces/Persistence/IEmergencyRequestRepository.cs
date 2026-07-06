using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IEmergencyRequestRepository
{
    Task<EmergencyRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<EmergencyRequest?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmergencyRequest>> GetOpenByCrewIdAsync(int crewId, CancellationToken cancellationToken = default);
    Task AddAsync(EmergencyRequest request, CancellationToken cancellationToken = default);
    Task AddSplitOfferAsync(EmergencySplitOffer offer, CancellationToken cancellationToken = default);
    Task AddGiftResponseAsync(EmergencyGiftResponse response, CancellationToken cancellationToken = default);
}
