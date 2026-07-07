namespace LiberationFleet.Server.Application.Common.Interfaces;

public interface ILiveKitTokenService
{
    string CreateRoomToken(string participantIdentity, string participantName, string liveKitRoomName);
    string CreateAdminToken();
    string GetWebSocketUrl();
}
