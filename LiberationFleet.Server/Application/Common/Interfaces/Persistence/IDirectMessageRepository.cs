using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IDirectMessageRepository
{
    Task<DirectConversation?> GetConversationBetweenUsersAsync(int userId, int otherUserId, CancellationToken cancellationToken = default);

    Task<DirectConversation> GetOrCreateConversationAsync(int userId, int otherUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, DateTime?>> GetLastMessageAtByFriendUserIdAsync(
        int userId,
        IReadOnlyList<int> friendUserIds,
        CancellationToken cancellationToken = default);

    Task AddMessageAsync(DirectMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DirectMessage>> GetLatestMessagesAsync(int conversationId, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DirectMessage>> GetMessagesBeforeIdAsync(
        int conversationId,
        int beforeMessageId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<DirectMessage?> GetMessageByIdWithAuthorAsync(int messageId, CancellationToken cancellationToken = default);
}
