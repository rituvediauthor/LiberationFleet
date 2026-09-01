using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface ILibraryTaskRepository
{
    Task AddTaskAsync(LibraryTask task, CancellationToken cancellationToken = default);

    Task AddInstancesAsync(IEnumerable<LibraryTaskInstance> instances, CancellationToken cancellationToken = default);

    Task<LibraryTask?> GetTaskByIdForCrewAsync(int taskId, int crewId, CancellationToken cancellationToken = default);

    Task<LibraryTask?> GetTrackedTaskByIdAsync(int taskId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryTask>> GetOpenTasksForCrewAsync(int crewId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryTaskInstance>> GetInstancesForTaskAsync(
        int taskId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryTaskInstance>> GetTrackedInstancesByIdsAsync(
        int taskId,
        IReadOnlyCollection<int> instanceIds,
        CancellationToken cancellationToken = default);

    Task CancelIncompleteInstancesAsync(int taskId, CancellationToken cancellationToken = default);

    Task ExpirePastOpenInstancesAsync(int taskId, DateTime utcNow, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetDistinctClaimantUserIdsAsync(int taskId, CancellationToken cancellationToken = default);
}
