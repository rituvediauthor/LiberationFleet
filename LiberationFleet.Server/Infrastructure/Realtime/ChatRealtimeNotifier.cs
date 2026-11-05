using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace LiberationFleet.Server.Infrastructure.Realtime;

public class ChatRealtimeNotifier(
    IHubContext<ChatHub> hubContext,
    ChatRoomConnectionRegistry roomConnections,
    IServiceScopeFactory scopeFactory) : IChatRealtimeNotifier
{
    public Task NotifyMessageSentAsync(int crewId, int roomId, ChatMessageDto message, CancellationToken cancellationToken = default) =>
        SendRoomMessageAsync(roomId, "MessageReceived", message, cancellationToken);

    public Task NotifyMessageUpdatedAsync(int crewId, int roomId, ChatMessageDto message, CancellationToken cancellationToken = default) =>
        SendRoomMessageAsync(roomId, "MessageUpdated", message, cancellationToken);

    public Task NotifyMessageDeletedAsync(int crewId, int roomId, int messageId, CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(ChatHub.RoomGroup(roomId))
            .SendAsync("MessageDeleted", new { roomId, messageId }, cancellationToken);

    public Task NotifyRoomCreatedAsync(int crewId, ChatRoomListItemDto room, CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(ChatHub.CrewGroup(crewId))
            .SendAsync("RoomCreated", room, cancellationToken);

    public Task NotifyFleetRoomCreatedAsync(int fleetId, ChatRoomListItemDto room, CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(ChatHub.FleetGroup(fleetId))
            .SendAsync("RoomCreated", room, cancellationToken);

    public Task NotifyRoomActivityUpdatedAsync(int crewId, int roomId, DateTime lastActivityAt, CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(ChatHub.CrewGroup(crewId))
            .SendAsync("RoomActivityUpdated", new { roomId, lastActivityAt }, cancellationToken);

    private async Task SendRoomMessageAsync(
        int roomId,
        string eventName,
        ChatMessageDto message,
        CancellationToken cancellationToken)
    {
        var authorUserId = message.DeliveryAuthorUserId > 0
            ? message.DeliveryAuthorUserId
            : message.AuthorUserId;
        var presentUserIds = roomConnections.UsersInRoom(roomId);
        if (authorUserId <= 0 || presentUserIds.Count == 0)
        {
            await hubContext.Clients
                .Group(ChatHub.RoomGroup(roomId))
                .SendAsync(eventName, message, cancellationToken);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var blockRepository = scope.ServiceProvider.GetRequiredService<IUserBlockRepository>();
        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(authorUserId, cancellationToken);
        var recipientIds = presentUserIds
            .Where(userId => !hiddenUserIds.Contains(userId))
            .Select(userId => userId.ToString())
            .ToList();
        if (recipientIds.Count == 0)
        {
            return;
        }

        await hubContext.Clients
            .Users(recipientIds)
            .SendAsync(eventName, message, cancellationToken);
    }
}
