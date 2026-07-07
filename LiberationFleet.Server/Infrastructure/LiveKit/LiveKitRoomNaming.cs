namespace LiberationFleet.Server.Infrastructure.LiveKit;

public static class LiveKitRoomNaming
{
    public static string ForVoiceChannel(int crewId, int chatRoomId) =>
        $"voice-crew-{crewId}-room-{chatRoomId}";
}
