using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class VoicePresenceRepository(ApplicationDbContext context) : IVoicePresenceRepository
{
    public Task<VoiceParticipantSession?> GetByIdAsync(int sessionId, CancellationToken cancellationToken = default) =>
        context.VoiceParticipantSessions
            .Include(session => session.User)
            .Include(session => session.ChatRoom)
            .FirstOrDefaultAsync(session => session.Id == sessionId, cancellationToken);

    public Task<VoiceParticipantSession?> GetActiveByUserAndCrewAsync(int userId, int crewId, CancellationToken cancellationToken = default) =>
        context.VoiceParticipantSessions
            .Include(session => session.User)
            .Include(session => session.ChatRoom)
            .FirstOrDefaultAsync(session => session.UserId == userId && session.CrewId == crewId, cancellationToken);

    public Task<VoiceParticipantSession?> GetByUserAndRoomAsync(int userId, int chatRoomId, CancellationToken cancellationToken = default) =>
        context.VoiceParticipantSessions
            .Include(session => session.User)
            .Include(session => session.ChatRoom)
            .FirstOrDefaultAsync(session => session.UserId == userId && session.ChatRoomId == chatRoomId, cancellationToken);

    public Task<VoiceParticipantSession?> GetByConnectionIdAsync(string connectionId, CancellationToken cancellationToken = default) =>
        context.VoiceParticipantSessions
            .Include(session => session.User)
            .Include(session => session.ChatRoom)
            .FirstOrDefaultAsync(session => session.ConnectionId == connectionId, cancellationToken);

    public async Task<IReadOnlyList<VoiceParticipantSession>> GetActiveByCrewIdAsync(int crewId, CancellationToken cancellationToken = default) =>
        await context.VoiceParticipantSessions
            .AsNoTracking()
            .Include(session => session.User)
            .Where(session => session.CrewId == crewId && session.ConnectionId != null)
            .OrderBy(session => session.ChatRoomId)
            .ThenBy(session => session.JoinedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(VoiceParticipantSession session, CancellationToken cancellationToken = default)
    {
        await context.VoiceParticipantSessions.AddAsync(session, cancellationToken);
    }

    public Task RemoveAsync(VoiceParticipantSession session, CancellationToken cancellationToken = default)
    {
        context.VoiceParticipantSessions.Remove(session);
        return Task.CompletedTask;
    }

    public async Task RemoveByUserAndCrewAsync(int userId, int crewId, CancellationToken cancellationToken = default)
    {
        var sessions = await context.VoiceParticipantSessions
            .Where(session => session.UserId == userId && session.CrewId == crewId)
            .ToListAsync(cancellationToken);

        context.VoiceParticipantSessions.RemoveRange(sessions);
    }
}
