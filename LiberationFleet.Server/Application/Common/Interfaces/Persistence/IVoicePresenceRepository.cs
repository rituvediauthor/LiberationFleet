using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IVoicePresenceRepository
{
    Task<VoiceParticipantSession?> GetByIdAsync(int sessionId, CancellationToken cancellationToken = default);
    Task<VoiceParticipantSession?> GetActiveByUserAndCrewAsync(int userId, int crewId, CancellationToken cancellationToken = default);
    Task<VoiceParticipantSession?> GetByUserAndRoomAsync(int userId, int chatRoomId, CancellationToken cancellationToken = default);
    Task<VoiceParticipantSession?> GetByConnectionIdAsync(string connectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VoiceParticipantSession>> GetActiveByCrewIdAsync(int crewId, CancellationToken cancellationToken = default);
    Task AddAsync(VoiceParticipantSession session, CancellationToken cancellationToken = default);
    Task RemoveAsync(VoiceParticipantSession session, CancellationToken cancellationToken = default);
    Task RemoveByUserAndCrewAsync(int userId, int crewId, CancellationToken cancellationToken = default);
}
