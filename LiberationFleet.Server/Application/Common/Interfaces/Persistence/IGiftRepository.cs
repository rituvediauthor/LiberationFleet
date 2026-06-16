using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IGiftRepository
{
    Task<IReadOnlyList<Gift>> GetLogByCrewIdAsync(int crewId, CancellationToken cancellationToken = default);
    Task<Gift?> GetByIdWithUsersAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Gift>> GetPendingMiddlemanGiftsAsync(int middlemanUserId, int crewId, CancellationToken cancellationToken = default);
    Task<bool> HasCompletedInitiatedGiftAsync(int initiatedGiftId, CancellationToken cancellationToken = default);
    Task AddAsync(Gift gift, CancellationToken cancellationToken = default);
    Task<UserGiftStats> GetUserGiftStatsAsync(int userId, CancellationToken cancellationToken = default);
}
