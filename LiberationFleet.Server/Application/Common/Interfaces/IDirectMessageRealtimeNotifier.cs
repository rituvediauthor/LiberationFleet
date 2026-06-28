using LiberationFleet.Server.Application.Features.Friends.Contracts;

namespace LiberationFleet.Server.Application.Common.Interfaces;

public interface IDirectMessageRealtimeNotifier
{
    Task NotifyDirectMessageSentAsync(
        int senderUserId,
        int recipientUserId,
        DirectMessageDto message,
        CancellationToken cancellationToken = default);

    Task NotifyDirectMessageUpdatedAsync(
        int editorUserId,
        int friendUserId,
        DirectMessageDto message,
        CancellationToken cancellationToken = default);
}
