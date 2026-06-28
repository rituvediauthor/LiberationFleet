using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace LiberationFleet.Server.Hubs;

[Authorize]
public class ChatHub(
    ICrewMembershipRepository membershipRepository,
    IChatRepository chatRepository) : Hub
{
    public async Task JoinCrew(int crewId)
    {
        var userId = GetUserId();
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, Context.ConnectionAborted);
        if (membership is null || membership.CrewId != crewId)
        {
            throw new HubException("You are not a member of this crew.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, CrewGroup(crewId));
    }

    public async Task LeaveCrew(int crewId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, CrewGroup(crewId));
    }

    public async Task JoinRoom(int roomId)
    {
        var userId = GetUserId();
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, Context.ConnectionAborted);
        if (membership is null)
        {
            throw new HubException("You are not in a crew.");
        }

        if (!await chatRepository.RoomBelongsToCrewAsync(roomId, membership.CrewId, Context.ConnectionAborted))
        {
            throw new HubException("Chat room not found.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomId));
    }

    public async Task LeaveRoom(int roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(roomId));
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        await base.OnConnectedAsync();
    }

    internal static string CrewGroup(int crewId) => $"crew:{crewId}";

    internal static string RoomGroup(int roomId) => $"room:{roomId}";

    internal static string UserGroup(int userId) => $"user:{userId}";

    private int GetUserId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;
        if (!int.TryParse(claim, out var userId))
        {
            throw new HubException("Unauthorized.");
        }

        return userId;
    }
}
