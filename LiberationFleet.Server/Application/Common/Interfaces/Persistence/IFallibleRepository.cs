namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IFallibleRepository
{
    Task RecordClickAsync(int? userId, CancellationToken cancellationToken = default);
}
