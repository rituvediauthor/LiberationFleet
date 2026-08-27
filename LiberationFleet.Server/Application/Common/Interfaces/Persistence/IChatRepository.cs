using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IChatRepository
{
    Task<ChatRoom?> GetRoomByIdAsync(int roomId, CancellationToken cancellationToken = default);
    Task<ChatRoom?> GetRoomByIdWithAuthorAsync(int roomId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatRoom>> GetRoomsByCrewIdAsync(int crewId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatRoom>> GetRoomsByFleetIdAsync(int fleetId, CancellationToken cancellationToken = default);
    Task<UserChatChannelOrder?> GetPersonalOrderAsync(
        int userId,
        int? crewId,
        int? fleetId,
        CancellationToken cancellationToken = default);
    Task UpsertPersonalOrderAsync(
        int userId,
        int? crewId,
        int? fleetId,
        string orderedRoomIdsJson,
        CancellationToken cancellationToken = default);
    Task UpdateRoomSortOrdersAsync(
        IReadOnlyList<int> orderedRoomIds,
        int? crewId,
        int? fleetId,
        CancellationToken cancellationToken = default);
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
    Task<ChatMessageLike?> GetMessageLikeAsync(int userId, int messageId, CancellationToken cancellationToken = default);
    Task AddMessageLikeAsync(ChatMessageLike like, CancellationToken cancellationToken = default);
    Task<Dictionary<int, int>> GetActiveLikeCountsForMessagesAsync(
        IEnumerable<int> messageIds,
        CancellationToken cancellationToken = default);
    Task<HashSet<int>> GetActiveLikedMessageIdsByUserAsync(
        int userId,
        IEnumerable<int> messageIds,
        CancellationToken cancellationToken = default);
}
