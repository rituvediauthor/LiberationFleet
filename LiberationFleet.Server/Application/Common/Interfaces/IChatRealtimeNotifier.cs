using LiberationFleet.Server.Application.Features.Chats.Contracts;

namespace LiberationFleet.Server.Application.Common.Interfaces;

public interface IChatRealtimeNotifier
{
    Task NotifyMessageSentAsync(int crewId, int roomId, ChatMessageDto message, CancellationToken cancellationToken = default);

    Task NotifyMessageUpdatedAsync(int crewId, int roomId, ChatMessageDto message, CancellationToken cancellationToken = default);

    Task NotifyRoomCreatedAsync(int crewId, ChatRoomListItemDto room, CancellationToken cancellationToken = default);

    Task NotifyRoomActivityUpdatedAsync(int crewId, int roomId, DateTime lastActivityAt, CancellationToken cancellationToken = default);
}
