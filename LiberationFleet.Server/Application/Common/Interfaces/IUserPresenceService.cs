namespace LiberationFleet.Server.Application.Common.Interfaces;

/// <summary>
/// Records authenticated product activity so crewmate/friend lists can show
/// recent presence (Active now) instead of only the last sign-in time.
/// </summary>
public interface IUserPresenceService
{
    /// <summary>
    /// Best-effort, throttled update of the user's last-active timestamp.
    /// Safe to call on every authenticated request; does not block the caller.
    /// </summary>
    void RecordActivity(int userId);
}
