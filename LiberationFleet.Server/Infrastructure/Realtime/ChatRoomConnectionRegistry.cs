using System.Collections.Concurrent;

namespace LiberationFleet.Server.Infrastructure.Realtime;

/// <summary>
/// Tracks which users currently have a chat hub connection joined to a room,
/// so live messages can skip people in a block relationship with the author.
/// </summary>
public sealed class ChatRoomConnectionRegistry
{
    private readonly ConcurrentDictionary<string, ConnectionRooms> _connections = new();
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, int>> _userCountsByRoom = new();

    public void Join(string connectionId, int roomId, int userId)
    {
        var connection = _connections.GetOrAdd(connectionId, _ => new ConnectionRooms(userId));
        if (!connection.RoomIds.TryAdd(roomId, 0))
        {
            return;
        }

        var users = _userCountsByRoom.GetOrAdd(roomId, _ => new ConcurrentDictionary<int, int>());
        users.AddOrUpdate(userId, 1, (_, count) => count + 1);
    }

    public void Leave(string connectionId, int roomId)
    {
        if (!_connections.TryGetValue(connectionId, out var connection))
        {
            return;
        }

        if (!connection.RoomIds.TryRemove(roomId, out _))
        {
            return;
        }

        Decrement(roomId, connection.UserId);
    }

    public void Disconnect(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var connection))
        {
            return;
        }

        foreach (var roomId in connection.RoomIds.Keys)
        {
            Decrement(roomId, connection.UserId);
        }
    }

    public IReadOnlyCollection<int> UsersInRoom(int roomId)
    {
        if (!_userCountsByRoom.TryGetValue(roomId, out var users) || users.IsEmpty)
        {
            return Array.Empty<int>();
        }

        return users.Keys.ToArray();
    }

    private void Decrement(int roomId, int userId)
    {
        if (!_userCountsByRoom.TryGetValue(roomId, out var users))
        {
            return;
        }

        while (users.TryGetValue(userId, out var count))
        {
            if (count <= 1)
            {
                if (users.TryRemove(new KeyValuePair<int, int>(userId, count)))
                {
                    return;
                }

                continue;
            }

            if (users.TryUpdate(userId, count - 1, count))
            {
                return;
            }
        }
    }

    private sealed class ConnectionRooms(int userId)
    {
        public int UserId { get; } = userId;
        public ConcurrentDictionary<int, byte> RoomIds { get; } = new();
    }
}
