using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace LiberationFleet.Server.Infrastructure.Realtime;

public class ChatRealtimeNotifier(IHubContext<ChatHub> hubContext) : IChatRealtimeNotifier
{
    public Task NotifyMessageSentAsync(int crewId, int roomId, ChatMessageDto message, CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(ChatHub.RoomGroup(roomId))
            .SendAsync("MessageReceived", message, cancellationToken);

    public Task NotifyMessageUpdatedAsync(int crewId, int roomId, ChatMessageDto message, CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(ChatHub.RoomGroup(roomId))
            .SendAsync("MessageUpdated", message, cancellationToken);

    public Task NotifyRoomCreatedAsync(int crewId, ChatRoomListItemDto room, CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(ChatHub.CrewGroup(crewId))
            .SendAsync("RoomCreated", room, cancellationToken);

    public Task NotifyRoomActivityUpdatedAsync(int crewId, int roomId, DateTime lastActivityAt, CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(ChatHub.CrewGroup(crewId))
            .SendAsync("RoomActivityUpdated", new { roomId, lastActivityAt }, cancellationToken);
}
