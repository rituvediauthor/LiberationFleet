using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Chats;

/// <summary>
/// Shared access rules for chat rooms. Crew rooms are visible to members of the owning crew;
/// fleet rooms are visible to every member of any crew currently in the fleet.
/// </summary>
public static class ChatRoomAccess
{
    public static async Task<bool> CanAccessRoomAsync(
        ChatRoom room,
        CrewMembership membership,
        IFleetRepository fleetRepository,
        CancellationToken cancellationToken)
    {
        if (room.CrewId.HasValue)
        {
            return room.CrewId.Value == membership.CrewId;
        }

        if (room.FleetId.HasValue)
        {
            return await fleetRepository.IsCrewInFleetAsync(membership.CrewId, room.FleetId.Value, cancellationToken);
        }

        return false;
    }
}
