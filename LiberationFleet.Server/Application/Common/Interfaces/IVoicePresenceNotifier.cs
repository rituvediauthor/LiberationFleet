using LiberationFleet.Server.Application.Features.Chats.Voice.Contracts;

namespace LiberationFleet.Server.Application.Common.Interfaces;

public interface IVoicePresenceNotifier
{
    Task NotifyPresenceUpdatedAsync(int crewId, CancellationToken cancellationToken = default);
    Task NotifyStateUpdatedAsync(int crewId, VoiceParticipantDto participant, CancellationToken cancellationToken = default);
}
