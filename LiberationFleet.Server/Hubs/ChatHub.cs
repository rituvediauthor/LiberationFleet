using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace LiberationFleet.Server.Hubs;

[Authorize]
public class ChatHub(
    ICrewMembershipRepository membershipRepository,
    IChatRepository chatRepository,
    IFleetRepository fleetRepository,
    IUserRepository userRepository,
    IFriendshipRepository friendshipRepository,
    ILibraryRepository libraryRepository) : Hub
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

    public async Task JoinFleet(int fleetId)
    {
        var userId = GetUserId();
        if (!await fleetRepository.IsUserInFleetAsync(userId, fleetId, Context.ConnectionAborted))
        {
            throw new HubException("You are not a member of this fleet.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, FleetGroup(fleetId));
    }

    public async Task LeaveFleet(int fleetId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, FleetGroup(fleetId));
    }

    public async Task JoinRoom(int roomId)
    {
        var userId = GetUserId();
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, Context.ConnectionAborted);
        if (membership is null)
        {
            throw new HubException("You are not in a crew.");
        }

        var room = await chatRepository.GetRoomByIdAsync(roomId, Context.ConnectionAborted);
        if (room is null || room.IsDeleted
            || !await ChatRoomAccess.CanAccessRoomAsync(
                room,
                membership,
                fleetRepository,
                Context.ConnectionAborted))
        {
            throw new HubException("Chat room not found.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomId));
    }

    public async Task LeaveRoom(int roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(roomId));
    }

    /// <summary>
    /// Ephemeral typing signal for a chat room. Not persisted.
    /// When <paramref name="isAnonymous"/> is true, identity is hidden like anonymous messages.
    /// </summary>
    public async Task SendRoomTyping(int roomId, bool isTyping, bool isAnonymous)
    {
        var userId = GetUserId();
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, Context.ConnectionAborted);
        if (membership is null)
        {
            throw new HubException("You are not in a crew.");
        }

        var room = await chatRepository.GetRoomByIdAsync(roomId, Context.ConnectionAborted);
        if (room is null || room.IsDeleted
            || !await ChatRoomAccess.CanAccessRoomAsync(
                room,
                membership,
                fleetRepository,
                Context.ConnectionAborted))
        {
            throw new HubException("Chat room not found.");
        }

        string displayName;
        int? payloadUserId;
        if (isAnonymous)
        {
            displayName = "Anonymous";
            payloadUserId = null;
        }
        else
        {
            var user = await userRepository.GetByIdAsync(userId, Context.ConnectionAborted);
            displayName = string.IsNullOrWhiteSpace(user?.Username) ? "Someone" : user!.Username;
            payloadUserId = userId;
        }

        await Clients.OthersInGroup(RoomGroup(roomId)).SendAsync(
            "Typing",
            new
            {
                scope = "room",
                roomId,
                userId = payloadUserId,
                displayName,
                isAnonymous,
                isTyping
            },
            Context.ConnectionAborted);
    }

    /// <summary>Ephemeral typing signal for a friend DM conversation.</summary>
    public async Task SendDirectTyping(int friendUserId, bool isTyping)
    {
        var userId = GetUserId();
        if (friendUserId <= 0 || friendUserId == userId)
        {
            throw new HubException("Invalid friend.");
        }

        var friendship = await friendshipRepository.GetBetweenUsersAsync(
            userId,
            friendUserId,
            Context.ConnectionAborted);
        if (friendship is null || friendship.Status != FriendshipStatus.Accepted)
        {
            throw new HubException("You can only message accepted friends.");
        }

        var user = await userRepository.GetByIdAsync(userId, Context.ConnectionAborted);
        var displayName = string.IsNullOrWhiteSpace(user?.Username) ? "Someone" : user!.Username;

        await Clients.Group(UserGroup(friendUserId)).SendAsync(
            "Typing",
            new
            {
                scope = "direct",
                friendUserId = userId,
                userId,
                displayName,
                isAnonymous = false,
                isTyping
            },
            Context.ConnectionAborted);
    }

    /// <summary>Ephemeral typing signal for a Library of Things request thread.</summary>
    public async Task SendLibraryRequestTyping(int requestId, bool isTyping)
    {
        var userId = GetUserId();
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, Context.ConnectionAborted);
        if (membership is null)
        {
            throw new HubException("You are not in a crew.");
        }

        var libraryRequest = await libraryRepository.GetRequestByIdForCrewAsync(
            requestId,
            membership.CrewId,
            Context.ConnectionAborted);
        if (libraryRequest is null || !LibraryRequestAccess.CanMessage(libraryRequest, userId))
        {
            throw new HubException("Request not found.");
        }

        var recipientUserId = LibraryRequestAccess.IsRequester(libraryRequest, userId)
            ? LibraryRequestAccess.GetPossessorUserId(libraryRequest)
            : libraryRequest.RequesterUserId;
        if (recipientUserId <= 0 || recipientUserId == userId)
        {
            return;
        }

        var user = await userRepository.GetByIdAsync(userId, Context.ConnectionAborted);
        var displayName = string.IsNullOrWhiteSpace(user?.Username) ? "Someone" : user!.Username;

        await Clients.Group(UserGroup(recipientUserId)).SendAsync(
            "Typing",
            new
            {
                scope = "libraryRequest",
                requestId,
                userId,
                displayName,
                isAnonymous = false,
                isTyping
            },
            Context.ConnectionAborted);
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        await base.OnConnectedAsync();
    }

    internal static string CrewGroup(int crewId) => $"crew:{crewId}";

    internal static string FleetGroup(int fleetId) => $"fleet:{fleetId}";

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
