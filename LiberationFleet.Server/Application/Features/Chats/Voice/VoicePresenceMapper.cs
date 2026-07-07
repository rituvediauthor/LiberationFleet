using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats.Voice.Contracts;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Chats.Voice;

public static class VoicePresenceMapper
{
    public static VoiceParticipantDto MapParticipant(VoiceParticipantSession session) =>
        new()
        {
            SessionId = session.Id,
            UserId = session.UserId,
            Username = session.User?.Username ?? string.Empty,
            ChatRoomId = session.ChatRoomId,
            IsMuted = session.IsMuted || session.IsServerMuted,
            IsDeafened = session.IsDeafened,
            IsSpeaking = session.IsSpeaking,
            IsServerMuted = session.IsServerMuted,
            JoinedAt = session.JoinedAt
        };

    public static async Task<VoicePresenceSnapshotResponse> BuildSnapshotAsync(
        IVoicePresenceRepository repository,
        int crewId,
        CancellationToken cancellationToken = default)
    {
        var sessions = await repository.GetActiveByCrewIdAsync(crewId, cancellationToken);
        var rooms = sessions
            .GroupBy(session => session.ChatRoomId)
            .Select(group => new VoiceRoomPresenceDto
            {
                ChatRoomId = group.Key,
                Participants = group.Select(MapParticipant).ToList()
            })
            .OrderBy(room => room.ChatRoomId)
            .ToList();

        return new VoicePresenceSnapshotResponse
        {
            Success = true,
            Message = "Voice presence loaded.",
            Rooms = rooms
        };
    }
}
