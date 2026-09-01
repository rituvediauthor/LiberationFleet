using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class LibraryTaskRepository(ApplicationDbContext context) : ILibraryTaskRepository
{
    public async Task AddTaskAsync(LibraryTask task, CancellationToken cancellationToken = default) =>
        await context.LibraryTasks.AddAsync(task, cancellationToken);

    public async Task AddInstancesAsync(
        IEnumerable<LibraryTaskInstance> instances,
        CancellationToken cancellationToken = default) =>
        await context.LibraryTaskInstances.AddRangeAsync(instances, cancellationToken);

    public async Task<LibraryTask?> GetTaskByIdForCrewAsync(
        int taskId,
        int crewId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryTasks
            .AsNoTracking()
            .Include(t => t.CreatorUser)
            .Include(t => t.Instances)
                .ThenInclude(i => i.ClaimedByUser)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.CrewId == crewId, cancellationToken);

    public async Task<LibraryTask?> GetTrackedTaskByIdAsync(
        int taskId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryTasks
            .Include(t => t.CreatorUser)
            .Include(t => t.Instances)
                .ThenInclude(i => i.ClaimedByUser)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

    public async Task<IReadOnlyList<LibraryTask>> GetOpenTasksForCrewAsync(
        int crewId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryTasks
            .AsNoTracking()
            .Include(t => t.CreatorUser)
            .Include(t => t.Instances)
            .Where(t => t.CrewId == crewId && !t.IsClosed)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LibraryTaskInstance>> GetInstancesForTaskAsync(
        int taskId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryTaskInstances
            .AsNoTracking()
            .Include(i => i.ClaimedByUser)
            .Where(i => i.TaskId == taskId)
            .OrderBy(i => i.ScheduledAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LibraryTaskInstance>> GetTrackedInstancesByIdsAsync(
        int taskId,
        IReadOnlyCollection<int> instanceIds,
        CancellationToken cancellationToken = default) =>
        await context.LibraryTaskInstances
            .Include(i => i.ClaimedByUser)
            .Where(i => i.TaskId == taskId && instanceIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

    public async Task CancelIncompleteInstancesAsync(int taskId, CancellationToken cancellationToken = default)
    {
        var incomplete = await context.LibraryTaskInstances
            .Where(i => i.TaskId == taskId
                && i.Status != LibraryTaskInstanceStatus.Confirmed
                && i.Status != LibraryTaskInstanceStatus.Cancelled)
            .ToListAsync(cancellationToken);

        foreach (var instance in incomplete)
        {
            instance.Status = LibraryTaskInstanceStatus.Cancelled;
            instance.ClaimedByUserId = null;
            instance.ClaimedAt = null;
            instance.CompletedAt = null;
        }
    }

    public async Task ExpirePastOpenInstancesAsync(
        int taskId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var pastOpen = await context.LibraryTaskInstances
            .Where(i => i.TaskId == taskId
                && i.Status == LibraryTaskInstanceStatus.Open
                && i.ScheduledAt < utcNow)
            .ToListAsync(cancellationToken);

        foreach (var instance in pastOpen)
        {
            instance.Status = LibraryTaskInstanceStatus.Cancelled;
        }
    }

    public async Task<IReadOnlyList<int>> GetDistinctClaimantUserIdsAsync(
        int taskId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryTaskInstances
            .AsNoTracking()
            .Where(i => i.TaskId == taskId
                && i.ClaimedByUserId.HasValue
                && (i.Status == LibraryTaskInstanceStatus.Claimed
                    || i.Status == LibraryTaskInstanceStatus.AwaitingConfirmation))
            .Select(i => i.ClaimedByUserId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
}
