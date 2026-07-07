namespace LiberationFleet.Server.Application.Common.Interfaces;

public interface ILiveKitAdminService
{
    Task RemoveParticipantAsync(string liveKitRoomName, string participantIdentity, CancellationToken cancellationToken = default);
}
