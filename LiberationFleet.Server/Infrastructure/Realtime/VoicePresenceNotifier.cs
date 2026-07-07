using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats.Voice;
using LiberationFleet.Server.Application.Features.Chats.Voice.Contracts;
using LiberationFleet.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace LiberationFleet.Server.Infrastructure.Realtime;

public class VoicePresenceNotifier(
    IHubContext<VoiceHub> hubContext,
    IVoicePresenceRepository voicePresenceRepository) : IVoicePresenceNotifier
{
    public async Task NotifyPresenceUpdatedAsync(int crewId, CancellationToken cancellationToken = default)
    {
        var snapshot = await VoicePresenceMapper.BuildSnapshotAsync(voicePresenceRepository, crewId, cancellationToken);
        await hubContext.Clients
            .Group(VoiceHub.CrewGroup(crewId))
            .SendAsync("VoicePresenceUpdated", snapshot, cancellationToken);
    }

    public Task NotifyStateUpdatedAsync(int crewId, VoiceParticipantDto participant, CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(VoiceHub.CrewGroup(crewId))
            .SendAsync("VoiceStateUpdated", participant, cancellationToken);
}
