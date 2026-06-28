using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Features.Friends.Contracts;
using LiberationFleet.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace LiberationFleet.Server.Infrastructure.Realtime;

public class DirectMessageRealtimeNotifier(IHubContext<ChatHub> hubContext) : IDirectMessageRealtimeNotifier
{
    public Task NotifyDirectMessageSentAsync(
        int senderUserId,
        int recipientUserId,
        DirectMessageDto message,
        CancellationToken cancellationToken = default)
    {
        var recipientPayload = new
        {
            friendUserId = senderUserId,
            message
        };

        var senderPayload = new
        {
            friendUserId = recipientUserId,
            message
        };

        return Task.WhenAll(
            hubContext.Clients
                .Group(ChatHub.UserGroup(recipientUserId))
                .SendAsync("DirectMessageReceived", recipientPayload, cancellationToken),
            hubContext.Clients
                .Group(ChatHub.UserGroup(senderUserId))
                .SendAsync("DirectMessageReceived", senderPayload, cancellationToken));
    }

    public Task NotifyDirectMessageUpdatedAsync(
        int editorUserId,
        int friendUserId,
        DirectMessageDto message,
        CancellationToken cancellationToken = default)
    {
        var editorPayload = new
        {
            friendUserId,
            message
        };

        var friendPayload = new
        {
            friendUserId = editorUserId,
            message
        };

        return Task.WhenAll(
            hubContext.Clients
                .Group(ChatHub.UserGroup(editorUserId))
                .SendAsync("DirectMessageUpdated", editorPayload, cancellationToken),
            hubContext.Clients
                .Group(ChatHub.UserGroup(friendUserId))
                .SendAsync("DirectMessageUpdated", friendPayload, cancellationToken));
    }
}
