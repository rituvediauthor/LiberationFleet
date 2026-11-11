namespace LiberationFleet.Server.Application.Common.Interfaces;

/// <summary>
/// Queues crew-wide priority / contribution recalculation off the request thread.
/// </summary>
public interface ICrewContributionRefreshScheduler
{
    void Schedule(int crewId);
}
