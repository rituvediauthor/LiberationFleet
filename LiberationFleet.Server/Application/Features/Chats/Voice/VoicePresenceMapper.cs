using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats.Voice.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Application.Common;

namespace LiberationFleet.Server.Application.Features.Chats.Voice;

public static class VoicePresenceMapper
{
    public static VoiceParticipantDto MapParticipant(VoiceParticipantSession session) =>
        new()
        {
            SessionId = session.Id,
            UserId = session.UserId,
            Username = UserDisplay.Name(session.User),
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
        CancellationToken cancellationToken = default,
        IReadOnlySet<int>? hiddenUserIds = null)
    {
        var sessions = await repository.GetActiveByCrewIdAsync(crewId, cancellationToken);
        if (hiddenUserIds is { Count: > 0 })
        {
            sessions = sessions.Where(session => !hiddenUserIds.Contains(session.UserId)).ToList();
        }

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
