using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class ChatRepository : IChatRepository
{
    private readonly ApplicationDbContext _context;

    public ChatRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<ChatRoom?> GetRoomByIdAsync(int roomId, CancellationToken cancellationToken = default) =>
        _context.ChatRooms.FirstOrDefaultAsync(r => r.Id == roomId && !r.IsDeleted, cancellationToken);

    public Task<ChatRoom?> GetRoomByIdWithAuthorAsync(int roomId, CancellationToken cancellationToken = default) =>
        _context.ChatRooms
            .Include(r => r.CreatedByUser)
            .FirstOrDefaultAsync(r => r.Id == roomId && !r.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<ChatRoom>> GetRoomsByCrewIdAsync(int crewId, CancellationToken cancellationToken = default) =>
        await _context.ChatRooms
            .AsNoTracking()
            .Include(r => r.CreatedByUser)
            .Where(r => r.CrewId == crewId && !r.IsDeleted)
            .OrderBy(r => r.SortOrder)
            .ThenByDescending(r => r.LastActivityAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ChatRoom>> GetRoomsByFleetIdAsync(int fleetId, CancellationToken cancellationToken = default) =>
        await _context.ChatRooms
            .AsNoTracking()
            .Include(r => r.CreatedByUser)
            .Where(r => r.FleetId == fleetId && !r.IsDeleted)
            .OrderBy(r => r.SortOrder)
            .ThenByDescending(r => r.LastActivityAt)
            .ToListAsync(cancellationToken);

    public Task<UserChatChannelOrder?> GetPersonalOrderAsync(
        int userId,
        int? crewId,
        int? fleetId,
        CancellationToken cancellationToken = default) =>
        _context.UserChatChannelOrders.FirstOrDefaultAsync(
            order => order.UserId == userId
                && order.CrewId == crewId
                && order.FleetId == fleetId,
            cancellationToken);

    public async Task UpsertPersonalOrderAsync(
        int userId,
        int? crewId,
        int? fleetId,
        string orderedRoomIdsJson,
        CancellationToken cancellationToken = default)
    {
        var order = await GetPersonalOrderAsync(userId, crewId, fleetId, cancellationToken);
        if (order is null)
        {
            await _context.UserChatChannelOrders.AddAsync(new UserChatChannelOrder
            {
                UserId = userId,
                CrewId = crewId,
                FleetId = fleetId,
                OrderedRoomIdsJson = orderedRoomIdsJson,
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken);
            return;
        }

        order.OrderedRoomIdsJson = orderedRoomIdsJson;
        order.UpdatedAt = DateTime.UtcNow;
    }

    public async Task UpdateRoomSortOrdersAsync(
        IReadOnlyList<int> orderedRoomIds,
        int? crewId,
        int? fleetId,
        CancellationToken cancellationToken = default)
    {
        var orderByRoomId = orderedRoomIds
            .Select((roomId, index) => new { roomId, index })
            .ToDictionary(item => item.roomId, item => item.index);

        var rooms = await _context.ChatRooms
            .Where(room => !room.IsDeleted
                && room.CrewId == crewId
                && room.FleetId == fleetId)
            .OrderBy(room => room.SortOrder)
            .ThenByDescending(room => room.LastActivityAt)
            .ToListAsync(cancellationToken);

        var sortedRooms = rooms
            .Select((room, existingIndex) => new { room, existingIndex })
            .OrderBy(item => orderByRoomId.TryGetValue(item.room.Id, out var index) ? index : int.MaxValue)
            .ThenBy(item => item.existingIndex)
            .Select(item => item.room)
            .ToList();

        for (var index = 0; index < sortedRooms.Count; index++)
        {
            sortedRooms[index].SortOrder = index;
        }
    }

    public async Task AddRoomAsync(ChatRoom room, CancellationToken cancellationToken = default) =>
        await _context.ChatRooms.AddAsync(room, cancellationToken);

    public async Task AddMessageAsync(ChatRoomMessage message, CancellationToken cancellationToken = default) =>
        await _context.ChatRoomMessages.AddAsync(message, cancellationToken);

    public async Task<IReadOnlyList<ChatRoomMessage>> GetLatestMessagesAsync(
        int roomId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var messages = await _context.ChatRoomMessages
            .AsNoTracking()
            .Include(m => m.AuthorUser)
            .Where(m => m.ChatRoomId == roomId && !m.IsDeleted)
            .OrderByDescending(m => m.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        messages.Reverse();
        return messages;
    }

    public async Task<IReadOnlyList<ChatRoomMessage>> GetMessagesBeforeIdAsync(
        int roomId,
        int beforeMessageId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var messages = await _context.ChatRoomMessages
            .AsNoTracking()
            .Include(m => m.AuthorUser)
            .Where(m => m.ChatRoomId == roomId && !m.IsDeleted && m.Id < beforeMessageId)
            .OrderByDescending(m => m.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        messages.Reverse();
        return messages;
    }

    public Task<ChatRoomMessage?> GetMessageByIdWithAuthorAsync(int messageId, CancellationToken cancellationToken = default) =>
        _context.ChatRoomMessages
            .Include(m => m.AuthorUser)
            .FirstOrDefaultAsync(m => m.Id == messageId && !m.IsDeleted, cancellationToken);

    public Task<bool> RoomBelongsToCrewAsync(int roomId, int crewId, CancellationToken cancellationToken = default) =>
        _context.ChatRooms.AnyAsync(r => r.Id == roomId && r.CrewId == crewId && !r.IsDeleted, cancellationToken);

    public Task<ChatMessageLike?> GetMessageLikeAsync(int userId, int messageId, CancellationToken cancellationToken = default) =>
        _context.ChatMessageLikes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.ChatRoomMessageId == messageId, cancellationToken);

    public async Task AddMessageLikeAsync(ChatMessageLike like, CancellationToken cancellationToken = default) =>
        await _context.ChatMessageLikes.AddAsync(like, cancellationToken);

    public async Task<Dictionary<int, int>> GetActiveLikeCountsForMessagesAsync(
        IEnumerable<int> messageIds,
        CancellationToken cancellationToken = default)
    {
        var ids = messageIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        return await _context.ChatMessageLikes
            .AsNoTracking()
            .Where(l => ids.Contains(l.ChatRoomMessageId) && l.RemovedAt == null)
            .GroupBy(l => l.ChatRoomMessageId)
            .Select(g => new { MessageId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MessageId, x => x.Count, cancellationToken);
    }

    public async Task<HashSet<int>> GetActiveLikedMessageIdsByUserAsync(
        int userId,
        IEnumerable<int> messageIds,
        CancellationToken cancellationToken = default)
    {
        var ids = messageIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var liked = await _context.ChatMessageLikes
            .AsNoTracking()
            .Where(l => l.UserId == userId && ids.Contains(l.ChatRoomMessageId) && l.RemovedAt == null)
            .Select(l => l.ChatRoomMessageId)
            .ToListAsync(cancellationToken);
        return liked.ToHashSet();
    }

    public async Task<IReadOnlyList<(int UserId, string Username, string? AvatarResourceId)>> GetActiveMessageLikersAsync(
        int messageId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.ChatMessageLikes
            .AsNoTracking()
            .Where(l => l.ChatRoomMessageId == messageId && l.RemovedAt == null)
            .OrderBy(l => l.CreatedAt)
            .Select(l => new { l.UserId, Username = l.User!.Username, l.User.AvatarResourceId })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.UserId, r.Username, r.AvatarResourceId)).ToList();
    }
}
