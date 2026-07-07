using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats.Voice;
using LiberationFleet.Server.Application.Features.Chats.Voice.Contracts;
using LiberationFleet.Server.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace LiberationFleet.Server.Hubs;

[Authorize]
public class VoiceHub(
    ICrewMembershipRepository membershipRepository,
    IChatRepository chatRepository,
    IVoicePresenceRepository voicePresenceRepository,
    IVoicePresenceNotifier voicePresenceNotifier,
    IUnitOfWork unitOfWork) : Hub
{
    public async Task JoinCrew(int crewId)
    {
        var userId = GetUserId();
        if (!await membershipRepository.IsUserInCrewAsync(userId, crewId, Context.ConnectionAborted))
        {
            throw new HubException("You are not a member of this crew.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, CrewGroup(crewId));
    }

    public async Task LeaveCrew(int crewId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, CrewGroup(crewId));
    }

    public async Task JoinVoice(int roomId)
    {
        var userId = GetUserId();
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, Context.ConnectionAborted);
        if (membership is null)
        {
            throw new HubException("You are not in a crew.");
        }

        if (!await chatRepository.RoomBelongsToCrewAsync(roomId, membership.CrewId, Context.ConnectionAborted))
        {
            throw new HubException("Voice channel not found.");
        }

        var session = await voicePresenceRepository.GetByUserAndRoomAsync(userId, roomId, Context.ConnectionAborted);
        if (session is null)
        {
            throw new HubException("Voice session not found. Join through the API first.");
        }

        session.ConnectionId = Context.ConnectionId;
        session.LastSeenAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, CrewGroup(membership.CrewId));
        await voicePresenceNotifier.NotifyPresenceUpdatedAsync(membership.CrewId, Context.ConnectionAborted);
    }

    public async Task LeaveVoice(int roomId)
    {
        var userId = GetUserId();
        var session = await voicePresenceRepository.GetByUserAndRoomAsync(userId, roomId, Context.ConnectionAborted);
        if (session is null)
        {
            return;
        }

        var crewId = session.CrewId;
        await voicePresenceRepository.RemoveAsync(session, Context.ConnectionAborted);
        await unitOfWork.SaveChangesAsync(Context.ConnectionAborted);
        await voicePresenceNotifier.NotifyPresenceUpdatedAsync(crewId, Context.ConnectionAborted);
    }

    public async Task UpdateVoiceState(int roomId, bool isMuted, bool isDeafened, bool isSpeaking)
    {
        var userId = GetUserId();
        var session = await voicePresenceRepository.GetByUserAndRoomAsync(userId, roomId, Context.ConnectionAborted);
        if (session is null)
        {
            throw new HubException("Voice session not found.");
        }

        session.IsMuted = isMuted;
        session.IsDeafened = isDeafened;
        session.IsSpeaking = isSpeaking;
        session.LastSeenAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(Context.ConnectionAborted);

        var participant = VoicePresenceMapper.MapParticipant(session);
        await voicePresenceNotifier.NotifyStateUpdatedAsync(session.CrewId, participant, Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var session = await voicePresenceRepository.GetByConnectionIdAsync(Context.ConnectionId, Context.ConnectionAborted);
        if (session is not null)
        {
            var crewId = session.CrewId;
            await voicePresenceRepository.RemoveAsync(session, Context.ConnectionAborted);
            await unitOfWork.SaveChangesAsync(Context.ConnectionAborted);
            await voicePresenceNotifier.NotifyPresenceUpdatedAsync(crewId, Context.ConnectionAborted);
        }

        await base.OnDisconnectedAsync(exception);
    }

    internal static string CrewGroup(int crewId) => $"crew:{crewId}";

    private int GetUserId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;
        if (!int.TryParse(claim, out var userId))
        {
            throw new HubException("Unauthorized.");
        }

        return userId;
    }
}
