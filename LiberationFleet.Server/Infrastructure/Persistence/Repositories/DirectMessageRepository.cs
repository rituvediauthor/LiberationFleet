using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class DirectMessageRepository : IDirectMessageRepository
{
    private readonly ApplicationDbContext _context;

    public DirectMessageRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<DirectConversation?> GetConversationBetweenUsersAsync(
        int userId,
        int otherUserId,
        CancellationToken cancellationToken = default)
    {
        var (low, high) = NormalizePair(userId, otherUserId);
        return _context.DirectConversations
            .FirstOrDefaultAsync(c => c.UserLowId == low && c.UserHighId == high, cancellationToken);
    }

    public async Task<DirectConversation> GetOrCreateConversationAsync(
        int userId,
        int otherUserId,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetConversationBetweenUsersAsync(userId, otherUserId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var (low, high) = NormalizePair(userId, otherUserId);
        var conversation = new DirectConversation
        {
            UserLowId = low,
            UserHighId = high,
            CreatedAt = DateTime.UtcNow
        };

        await _context.DirectConversations.AddAsync(conversation, cancellationToken);
        return conversation;
    }

    public async Task<IReadOnlyDictionary<int, DateTime?>> GetLastMessageAtByFriendUserIdAsync(
        int userId,
        IReadOnlyList<int> friendUserIds,
        CancellationToken cancellationToken = default)
    {
        if (friendUserIds.Count == 0)
        {
            return new Dictionary<int, DateTime?>();
        }

        var friendSet = friendUserIds.ToHashSet();
        var conversations = await _context.DirectConversations
            .Where(c => friendSet.Contains(c.UserLowId == userId ? c.UserHighId : c.UserLowId)
                && (c.UserLowId == userId || c.UserHighId == userId))
            .Select(c => new
            {
                FriendUserId = c.UserLowId == userId ? c.UserHighId : c.UserLowId,
                c.LastMessageAt
            })
            .ToListAsync(cancellationToken);

        return conversations.ToDictionary(c => c.FriendUserId, c => c.LastMessageAt);
    }

    public async Task AddMessageAsync(DirectMessage message, CancellationToken cancellationToken = default) =>
        await _context.DirectMessages.AddAsync(message, cancellationToken);

    public async Task<IReadOnlyList<DirectMessage>> GetLatestMessagesAsync(
        int conversationId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var messages = await _context.DirectMessages
            .Include(m => m.AuthorUser)
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
            .OrderByDescending(m => m.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        messages.Reverse();
        return messages;
    }

    public async Task<IReadOnlyList<DirectMessage>> GetMessagesBeforeIdAsync(
        int conversationId,
        int beforeMessageId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var messages = await _context.DirectMessages
            .Include(m => m.AuthorUser)
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted && m.Id < beforeMessageId)
            .OrderByDescending(m => m.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        messages.Reverse();
        return messages;
    }

    public Task<DirectMessage?> GetMessageByIdWithAuthorAsync(int messageId, CancellationToken cancellationToken = default) =>
        _context.DirectMessages
            .Include(m => m.AuthorUser)
            .FirstOrDefaultAsync(m => m.Id == messageId && !m.IsDeleted, cancellationToken);

    private static (int Low, int High) NormalizePair(int userId, int otherUserId) =>
        userId < otherUserId ? (userId, otherUserId) : (otherUserId, userId);
}
