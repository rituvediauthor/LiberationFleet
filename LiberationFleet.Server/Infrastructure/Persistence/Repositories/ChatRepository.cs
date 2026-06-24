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
            .Include(r => r.CreatedByUser)
            .Where(r => r.CrewId == crewId && !r.IsDeleted)
            .OrderByDescending(r => r.LastActivityAt)
            .ToListAsync(cancellationToken);

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
}
