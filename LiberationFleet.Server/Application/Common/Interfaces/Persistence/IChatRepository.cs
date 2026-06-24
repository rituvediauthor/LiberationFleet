using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IChatRepository
{
    Task<ChatRoom?> GetRoomByIdAsync(int roomId, CancellationToken cancellationToken = default);
    Task<ChatRoom?> GetRoomByIdWithAuthorAsync(int roomId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatRoom>> GetRoomsByCrewIdAsync(int crewId, CancellationToken cancellationToken = default);
    Task AddRoomAsync(ChatRoom room, CancellationToken cancellationToken = default);
    Task AddMessageAsync(ChatRoomMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatRoomMessage>> GetLatestMessagesAsync(
        int roomId,
        int limit,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatRoomMessage>> GetMessagesBeforeIdAsync(
        int roomId,
        int beforeMessageId,
        int limit,
        CancellationToken cancellationToken = default);
    Task<ChatRoomMessage?> GetMessageByIdWithAuthorAsync(int messageId, CancellationToken cancellationToken = default);
    Task<bool> RoomBelongsToCrewAsync(int roomId, int crewId, CancellationToken cancellationToken = default);
}
