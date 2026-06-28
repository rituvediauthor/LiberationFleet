namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface ICrewCleanupRepository
{
    Task CleanupCrewExceptGiftsAsync(int crewId, CancellationToken cancellationToken = default);
}
