namespace LiberationFleet.Server.Domain.Enums;

public enum MutedContentType
{
    ChatRoom = 1,
    Forum = 2,
    Friend = 4,
    /// <summary>Resource id is the crew id. Blocks post notifications in that crew space.</summary>
    CrewSpace = 8,
    /// <summary>Resource id is the fleet id. Blocks post notifications in that fleet space.</summary>
    FleetSpace = 16
}
