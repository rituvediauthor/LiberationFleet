using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class Friendship
{
    public int Id { get; set; }
    public int RequesterUserId { get; set; }
    public int AddresseeUserId { get; set; }
    /// <summary>min(Requester, Addressee) for unordered unique pair.</summary>
    public int UserLowId { get; set; }
    /// <summary>max(Requester, Addressee) for unordered unique pair.</summary>
    public int UserHighId { get; set; }
    public FriendshipStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }

    public User Requester { get; set; } = null!;
    public User Addressee { get; set; } = null!;

    public static void SetPairIds(Friendship friendship)
    {
        friendship.UserLowId = Math.Min(friendship.RequesterUserId, friendship.AddresseeUserId);
        friendship.UserHighId = Math.Max(friendship.RequesterUserId, friendship.AddresseeUserId);
    }
}
