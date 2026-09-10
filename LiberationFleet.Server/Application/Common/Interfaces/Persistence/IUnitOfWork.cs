namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops all tracked entity entries so a failed best-effort SaveChanges cannot poison
    /// a later save in the same request scope.
    /// </summary>
    void ClearTrackedChanges();
}
