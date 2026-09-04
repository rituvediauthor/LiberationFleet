using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.LibraryTasks;

public record GetLibraryTasksQuery : IRequest<LibraryTaskListResponse>;

public class GetLibraryTasksQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryTaskRepository taskRepository) : IRequestHandler<GetLibraryTasksQuery, LibraryTaskListResponse>
{
    public async Task<LibraryTaskListResponse> Handle(GetLibraryTasksQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryTaskListResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(
            currentUser.UserId.Value,
            cancellationToken);
        if (membership is null)
        {
            return new LibraryTaskListResponse { Success = false, Message = "You must be in a crew." };
        }

        var utcNow = DateTime.UtcNow;
        var tasks = await taskRepository.GetOpenTasksForCrewAsync(membership.CrewId, cancellationToken);
        var items = tasks
            .Where(t => t.HasDeadline)
            .Select(t => LibraryTaskMapper.ToListItem(t, utcNow))
            .Where(t => t.NextDueAt.HasValue)
            .OrderBy(t => t.NextDueAt)
            .ToList();

        return new LibraryTaskListResponse
        {
            Success = true,
            Message = "Quests loaded.",
            Items = items
        };
    }
}

public record GetNoDeadlineLibraryTasksQuery : IRequest<LibraryTaskListResponse>;

public class GetNoDeadlineLibraryTasksQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryTaskRepository taskRepository) : IRequestHandler<GetNoDeadlineLibraryTasksQuery, LibraryTaskListResponse>
{
    public async Task<LibraryTaskListResponse> Handle(
        GetNoDeadlineLibraryTasksQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryTaskListResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(
            currentUser.UserId.Value,
            cancellationToken);
        if (membership is null)
        {
            return new LibraryTaskListResponse { Success = false, Message = "You must be in a crew." };
        }

        var utcNow = DateTime.UtcNow;
        var tasks = await taskRepository.GetOpenTasksForCrewAsync(membership.CrewId, cancellationToken);
        var items = tasks
            .Where(t => !t.HasDeadline)
            .Select(t => LibraryTaskMapper.ToListItem(t, utcNow))
            .OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.TaskId)
            .ToList();

        return new LibraryTaskListResponse
        {
            Success = true,
            Message = "No-deadline quests loaded.",
            Items = items
        };
    }
}

public record GetLibraryTaskDetailQuery(int TaskId) : IRequest<LibraryTaskDetailResponse>;

public class GetLibraryTaskDetailQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryTaskRepository taskRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<GetLibraryTaskDetailQuery, LibraryTaskDetailResponse>
{
    public async Task<LibraryTaskDetailResponse> Handle(
        GetLibraryTaskDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryTaskDetailResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(
            currentUser.UserId.Value,
            cancellationToken);
        if (membership is null)
        {
            return new LibraryTaskDetailResponse { Success = false, Message = "You must be in a crew." };
        }

        var utcNow = DateTime.UtcNow;
        var task = await taskRepository.GetTrackedTaskByIdAsync(request.TaskId, cancellationToken);
        if (task is null || task.CrewId != membership.CrewId || task.IsClosed)
        {
            return new LibraryTaskDetailResponse { Success = false, Message = "Quest not found." };
        }

        if (task.HasDeadline)
        {
            await taskRepository.ExpirePastOpenInstancesAsync(request.TaskId, utcNow, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            task = await taskRepository.GetTaskByIdForCrewAsync(
                request.TaskId,
                membership.CrewId,
                cancellationToken);
            if (task is null || task.IsClosed)
            {
                return new LibraryTaskDetailResponse { Success = false, Message = "Quest not found." };
            }
        }

        return new LibraryTaskDetailResponse
        {
            Success = true,
            Message = "Quest loaded.",
            Task = LibraryTaskMapper.ToDetail(task, currentUser.UserId.Value, utcNow)
        };
    }
}

public record CreateLibraryTaskCommand(UpsertLibraryTaskRequest Body) : IRequest<LibraryTaskOperationResponse>;

public class CreateLibraryTaskCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryTaskRepository taskRepository,
    ICryptoRepository cryptoRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateLibraryTaskCommand, LibraryTaskOperationResponse>
{
    public async Task<LibraryTaskOperationResponse> Handle(
        CreateLibraryTaskCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var validation = ValidateUpsert(request.Body);
        if (validation is not null)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = validation };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(
            currentUser.UserId.Value,
            cancellationToken);
        if (membership is null)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "You must be in a crew." };
        }

        var utcNow = DateTime.UtcNow;
        var task = new LibraryTask
        {
            CrewId = membership.CrewId,
            CreatorUserId = currentUser.UserId.Value,
            Title = string.Empty,
            Details = string.Empty,
            HasEncryptedContent = true,
            Value = request.Body.Value,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
        LibraryTaskMapper.ApplyScheduleFields(task, request.Body);

        await taskRepository.AddTaskAsync(task, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var instances = LibraryTaskMapper.CreateInstances(task, utcNow);
        if (instances.Count == 0 && task.HasDeadline)
        {
            return new LibraryTaskOperationResponse
            {
                Success = false,
                Message = "Could not schedule any upcoming instances for this quest."
            };
        }

        if (instances.Count > 0)
        {
            await taskRepository.AddInstancesAsync(instances, cancellationToken);
        }
        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.LibraryTask,
            ResourceId = task.Id.ToString(),
            CrewId = membership.CrewId,
            AuthorUserId = currentUser.UserId.Value,
            KeyVersion = request.Body.KeyVersion <= 0 ? 1 : request.Body.KeyVersion,
            Nonce = request.Body.Nonce.Trim(),
            Ciphertext = request.Body.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LibraryTaskOperationResponse
        {
            Success = true,
            Message = "Quest created.",
            TaskId = task.Id
        };
    }

    internal static string? ValidateUpsert(UpsertLibraryTaskRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.Nonce) || string.IsNullOrWhiteSpace(body.Ciphertext))
        {
            return "Encrypted quest content is required.";
        }

        if (body.Value <= 0)
        {
            return "Value must be greater than zero.";
        }

        if (!body.HasDeadline)
        {
            return null;
        }

        if (!body.IsRecurring && !body.OneShotDueAt.HasValue)
        {
            return "Choose a due date for a one-time quest.";
        }

        if (body.IsRecurring)
        {
            var frequency = LibraryTaskMapper.ParseFrequency(body.Frequency, true);
            if (frequency == LibraryTaskRecurrenceFrequency.Weekly
                && body.DaySpecific
                && (body.WeekDays is null || body.WeekDays.Count == 0))
            {
                return "Select at least one day of the week.";
            }

            if (frequency == LibraryTaskRecurrenceFrequency.Monthly
                && body.DaySpecific
                && (body.MonthDays is null || body.MonthDays.Count == 0))
            {
                return "Select at least one day of the month.";
            }

            if (frequency == LibraryTaskRecurrenceFrequency.Yearly
                && (body.YearMonth is null || body.YearDay is null))
            {
                return "Choose a month and day for yearly quests.";
            }
        }

        return null;
    }
}

public record UpdateLibraryTaskCommand(int TaskId, UpsertLibraryTaskRequest Body)
    : IRequest<LibraryTaskOperationResponse>;

public class UpdateLibraryTaskCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryTaskRepository taskRepository,
    ICryptoRepository cryptoRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateLibraryTaskCommand, LibraryTaskOperationResponse>
{
    public async Task<LibraryTaskOperationResponse> Handle(
        UpdateLibraryTaskCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var validation = CreateLibraryTaskCommandHandler.ValidateUpsert(request.Body);
        if (validation is not null)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = validation };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(
            currentUser.UserId.Value,
            cancellationToken);
        if (membership is null)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "You must be in a crew." };
        }

        var task = await taskRepository.GetTrackedTaskByIdAsync(request.TaskId, cancellationToken);
        if (task is null || task.CrewId != membership.CrewId || task.IsClosed)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "Quest not found." };
        }

        if (task.CreatorUserId != currentUser.UserId.Value)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "Only the quest creator can edit it." };
        }

        var scheduleChanged = !LibraryTaskMapper.ScheduleEquals(task, request.Body);
        var claimantIds = scheduleChanged
            ? await taskRepository.GetDistinctClaimantUserIdsAsync(task.Id, cancellationToken)
            : Array.Empty<int>();

        var utcNow = DateTime.UtcNow;
        task.Title = string.Empty;
        task.Details = string.Empty;
        task.HasEncryptedContent = true;
        task.Value = request.Body.Value;
        task.UpdatedAt = utcNow;
        LibraryTaskMapper.ApplyScheduleFields(task, request.Body);

        if (scheduleChanged)
        {
            await taskRepository.CancelIncompleteInstancesAsync(task.Id, cancellationToken);
            var instances = LibraryTaskMapper.CreateInstances(task, utcNow);
            if (instances.Count == 0 && task.HasDeadline)
            {
                return new LibraryTaskOperationResponse
                {
                    Success = false,
                    Message = "Could not schedule any upcoming instances for this quest."
                };
            }

            if (instances.Count > 0)
            {
                await taskRepository.AddInstancesAsync(instances, cancellationToken);
            }
        }

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.LibraryTask,
            ResourceId = task.Id.ToString(),
            CrewId = membership.CrewId,
            AuthorUserId = currentUser.UserId.Value,
            KeyVersion = request.Body.KeyVersion <= 0 ? 1 : request.Body.KeyVersion,
            Nonce = request.Body.Nonce.Trim(),
            Ciphertext = request.Body.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (scheduleChanged)
        {
            foreach (var userId in claimantIds.Where(id => id != currentUser.UserId.Value))
            {
                await notificationService.NotifyUserAsync(new CreateNotificationRequest
                {
                    UserId = userId,
                    CrewId = membership.CrewId,
                    Kind = NotificationKind.LibraryTaskScheduleChanged,
                    Title = "Quest schedule updated",
                    Body = "A quest you claimed was rescheduled. Open it to claim a new instance if you still want to help.",
                    ActionUrl = $"/app/crew/library-of-things/tasks/{task.Id}",
                    RelatedEntityId = task.Id
                }, cancellationToken);
            }
        }

        return new LibraryTaskOperationResponse
        {
            Success = true,
            Message = "Quest updated.",
            TaskId = task.Id
        };
    }
}

public record DeleteLibraryTaskCommand(int TaskId) : IRequest<LibraryTaskOperationResponse>;

public class DeleteLibraryTaskCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryTaskRepository taskRepository,
    ICryptoRepository cryptoRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteLibraryTaskCommand, LibraryTaskOperationResponse>
{
    public async Task<LibraryTaskOperationResponse> Handle(
        DeleteLibraryTaskCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(
            currentUser.UserId.Value,
            cancellationToken);
        if (membership is null)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "You must be in a crew." };
        }

        var task = await taskRepository.GetTrackedTaskByIdAsync(request.TaskId, cancellationToken);
        if (task is null || task.CrewId != membership.CrewId || task.IsClosed)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "Quest not found." };
        }

        if (task.CreatorUserId != currentUser.UserId.Value)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "Only the quest creator can delete it." };
        }

        var claimantIds = await taskRepository.GetDistinctClaimantUserIdsAsync(task.Id, cancellationToken);
        await taskRepository.CancelIncompleteInstancesAsync(task.Id, cancellationToken);
        task.IsClosed = true;
        task.UpdatedAt = DateTime.UtcNow;

        if (task.HasEncryptedContent)
        {
            await cryptoRepository.DeleteEnvelopesAsync(
                EncryptedContentType.LibraryTask,
                [task.Id.ToString()],
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var userId in claimantIds.Where(id => id != currentUser.UserId.Value))
        {
            await notificationService.NotifyUserAsync(new CreateNotificationRequest
            {
                UserId = userId,
                CrewId = membership.CrewId,
                Kind = NotificationKind.LibraryTaskScheduleChanged,
                Title = "Quest deleted",
                Body = "A quest you claimed was deleted by its creator.",
                ActionUrl = "/app/crew/library-of-things/tasks",
                RelatedEntityId = task.Id
            }, cancellationToken);
        }

        return new LibraryTaskOperationResponse
        {
            Success = true,
            Message = "Quest deleted.",
            TaskId = task.Id
        };
    }
}

public record ClaimLibraryTaskInstancesCommand(int TaskId, IReadOnlyList<int> InstanceIds)
    : IRequest<LibraryTaskOperationResponse>;

public class ClaimLibraryTaskInstancesCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryTaskRepository taskRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ClaimLibraryTaskInstancesCommand, LibraryTaskOperationResponse>
{
    public async Task<LibraryTaskOperationResponse> Handle(
        ClaimLibraryTaskInstancesCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(
            currentUser.UserId.Value,
            cancellationToken);
        if (membership is null)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "You must be in a crew." };
        }

        var task = await taskRepository.GetTrackedTaskByIdAsync(request.TaskId, cancellationToken);
        if (task is null || task.CrewId != membership.CrewId || task.IsClosed)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "Quest not found." };
        }

        if (task.CreatorUserId == currentUser.UserId.Value)
        {
            return new LibraryTaskOperationResponse
            {
                Success = false,
                Message = "You cannot claim your own quest instances."
            };
        }

        var instances = await taskRepository.GetTrackedInstancesByIdsAsync(
            request.TaskId,
            request.InstanceIds,
            cancellationToken);
        if (instances.Count == 0 || instances.Any(i => i.Status != LibraryTaskInstanceStatus.Open))
        {
            return new LibraryTaskOperationResponse
            {
                Success = false,
                Message = "Select only unclaimed instances."
            };
        }

        var utcNow = DateTime.UtcNow;
        foreach (var instance in instances)
        {
            instance.Status = LibraryTaskInstanceStatus.Claimed;
            instance.ClaimedByUserId = currentUser.UserId.Value;
            instance.ClaimedAt = utcNow;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new LibraryTaskOperationResponse { Success = true, Message = "Instances claimed.", TaskId = task.Id };
    }
}

public record UnclaimLibraryTaskInstancesCommand(int TaskId, IReadOnlyList<int> InstanceIds)
    : IRequest<LibraryTaskOperationResponse>;

public class UnclaimLibraryTaskInstancesCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryTaskRepository taskRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UnclaimLibraryTaskInstancesCommand, LibraryTaskOperationResponse>
{
    public async Task<LibraryTaskOperationResponse> Handle(
        UnclaimLibraryTaskInstancesCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(
            currentUser.UserId.Value,
            cancellationToken);
        if (membership is null)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "You must be in a crew." };
        }

        var task = await taskRepository.GetTrackedTaskByIdAsync(request.TaskId, cancellationToken);
        if (task is null || task.CrewId != membership.CrewId)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "Quest not found." };
        }

        var instances = await taskRepository.GetTrackedInstancesByIdsAsync(
            request.TaskId,
            request.InstanceIds,
            cancellationToken);
        if (instances.Count == 0
            || instances.Any(i =>
                i.Status != LibraryTaskInstanceStatus.Claimed
                || i.ClaimedByUserId != currentUser.UserId.Value))
        {
            return new LibraryTaskOperationResponse
            {
                Success = false,
                Message = "Select only instances you have claimed."
            };
        }

        foreach (var instance in instances)
        {
            instance.Status = LibraryTaskInstanceStatus.Open;
            instance.ClaimedByUserId = null;
            instance.ClaimedAt = null;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new LibraryTaskOperationResponse { Success = true, Message = "Instances unclaimed.", TaskId = task.Id };
    }
}

public record CompleteLibraryTaskInstancesCommand(int TaskId, IReadOnlyList<int> InstanceIds)
    : IRequest<LibraryTaskOperationResponse>;

public class CompleteLibraryTaskInstancesCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryTaskRepository taskRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CompleteLibraryTaskInstancesCommand, LibraryTaskOperationResponse>
{
    public async Task<LibraryTaskOperationResponse> Handle(
        CompleteLibraryTaskInstancesCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(
            currentUser.UserId.Value,
            cancellationToken);
        if (membership is null)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "You must be in a crew." };
        }

        var task = await taskRepository.GetTrackedTaskByIdAsync(request.TaskId, cancellationToken);
        if (task is null || task.CrewId != membership.CrewId)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "Quest not found." };
        }

        var instances = await taskRepository.GetTrackedInstancesByIdsAsync(
            request.TaskId,
            request.InstanceIds,
            cancellationToken);
        if (instances.Count == 0
            || instances.Any(i =>
                i.Status != LibraryTaskInstanceStatus.Claimed
                || i.ClaimedByUserId != currentUser.UserId.Value))
        {
            return new LibraryTaskOperationResponse
            {
                Success = false,
                Message = "Select only instances you have claimed."
            };
        }

        var utcNow = DateTime.UtcNow;
        foreach (var instance in instances)
        {
            instance.Status = LibraryTaskInstanceStatus.AwaitingConfirmation;
            instance.CompletedAt = utcNow;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new LibraryTaskOperationResponse
        {
            Success = true,
            Message = "Marked complete — waiting for confirmation.",
            TaskId = task.Id
        };
    }
}

public record ConfirmLibraryTaskInstancesCommand(int TaskId, IReadOnlyList<int> InstanceIds)
    : IRequest<LibraryTaskConfirmResponse>;

public class ConfirmLibraryTaskInstancesCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryTaskRepository taskRepository,
    LibraryContributionGiftService contributionGiftService,
    IGiftRepository giftRepository,
    IMutualAidService mutualAidService,
    IUnitOfWork unitOfWork) : IRequestHandler<ConfirmLibraryTaskInstancesCommand, LibraryTaskConfirmResponse>
{
    public async Task<LibraryTaskConfirmResponse> Handle(
        ConfirmLibraryTaskInstancesCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryTaskConfirmResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(
            currentUser.UserId.Value,
            cancellationToken);
        if (membership is null)
        {
            return new LibraryTaskConfirmResponse { Success = false, Message = "You must be in a crew." };
        }

        var task = await taskRepository.GetTrackedTaskByIdAsync(request.TaskId, cancellationToken);
        if (task is null || task.CrewId != membership.CrewId)
        {
            return new LibraryTaskConfirmResponse { Success = false, Message = "Quest not found." };
        }

        if (task.CreatorUserId != currentUser.UserId.Value)
        {
            return new LibraryTaskConfirmResponse
            {
                Success = false,
                Message = "Only the quest creator can confirm completion."
            };
        }

        var instances = await taskRepository.GetTrackedInstancesByIdsAsync(
            request.TaskId,
            request.InstanceIds,
            cancellationToken);
        if (instances.Count == 0
            || instances.Any(i =>
                i.Status != LibraryTaskInstanceStatus.AwaitingConfirmation
                || !i.ClaimedByUserId.HasValue))
        {
            return new LibraryTaskConfirmResponse
            {
                Success = false,
                Message = "Select only completed instances awaiting confirmation."
            };
        }

        var gifts = new List<LibraryCreatorContributionGiftDto>();
        var utcNow = DateTime.UtcNow;
        foreach (var instance in instances)
        {
            var completerId = instance.ClaimedByUserId!.Value;
            var completerName = instance.ClaimedByUser?.Username ?? "Crewmate";
            var gift = await contributionGiftService.TryAwardTaskCompletionAsync(
                membership.CrewId,
                "Library quest",
                task.Value,
                completerId,
                completerName,
                task.CreatorUserId,
                task.CreatorUser?.Username ?? "Crewmate",
                cancellationToken);
            if (gift is not null)
            {
                instance.ContributionGiftId = gift.GiftId;
                gifts.Add(LibraryTaskMapper.ToGiftDto(gift));
            }

            instance.Status = LibraryTaskInstanceStatus.Confirmed;
            instance.ConfirmedAt = utcNow;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (var gift in gifts)
        {
            var receptionRecord = await giftRepository.GetByIdWithUsersAsync(gift.GiftId, cancellationToken);
            if (receptionRecord is not null)
            {
                await mutualAidService.ApplyGiftReceptionAsync(receptionRecord, cancellationToken);
            }
        }

        if (gifts.Count > 0)
        {
            await mutualAidService.OnCrewContributionsChangedAsync(membership.CrewId, cancellationToken);
        }

        if (!task.HasDeadline && task.DeleteOnCompletion)
        {
            task.IsClosed = true;
            task.UpdatedAt = utcNow;
            await taskRepository.CancelIncompleteInstancesAsync(task.Id, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new LibraryTaskConfirmResponse
        {
            Success = true,
            Message = !task.HasDeadline && task.DeleteOnCompletion
                ? "Completion confirmed. Quest removed from the board."
                : "Completion confirmed.",
            ContributionGifts = gifts,
            TaskClosed = !task.HasDeadline && task.DeleteOnCompletion
        };
    }
}

public record RejectLibraryTaskInstancesCommand(int TaskId, IReadOnlyList<int> InstanceIds)
    : IRequest<LibraryTaskOperationResponse>;

public class RejectLibraryTaskInstancesCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryTaskRepository taskRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<RejectLibraryTaskInstancesCommand, LibraryTaskOperationResponse>
{
    public async Task<LibraryTaskOperationResponse> Handle(
        RejectLibraryTaskInstancesCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(
            currentUser.UserId.Value,
            cancellationToken);
        if (membership is null)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "You must be in a crew." };
        }

        var task = await taskRepository.GetTrackedTaskByIdAsync(request.TaskId, cancellationToken);
        if (task is null || task.CrewId != membership.CrewId)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "Quest not found." };
        }

        if (task.CreatorUserId != currentUser.UserId.Value)
        {
            return new LibraryTaskOperationResponse
            {
                Success = false,
                Message = "Only the quest creator can mark instances incomplete."
            };
        }

        var instances = await taskRepository.GetTrackedInstancesByIdsAsync(
            request.TaskId,
            request.InstanceIds,
            cancellationToken);
        if (instances.Count == 0
            || instances.Any(i => i.Status != LibraryTaskInstanceStatus.AwaitingConfirmation))
        {
            return new LibraryTaskOperationResponse
            {
                Success = false,
                Message = "Select only completed instances awaiting confirmation."
            };
        }

        foreach (var instance in instances)
        {
            if (!task.HasDeadline)
            {
                instance.Status = LibraryTaskInstanceStatus.Cancelled;
                instance.ClaimedByUserId = null;
                instance.ClaimedAt = null;
                instance.CompletedAt = null;
                continue;
            }

            instance.Status = LibraryTaskInstanceStatus.Open;
            instance.ClaimedByUserId = null;
            instance.ClaimedAt = null;
            instance.CompletedAt = null;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new LibraryTaskOperationResponse
        {
            Success = true,
            Message = "Marked incomplete.",
            TaskId = task.Id
        };
    }
}

public record CompleteNoDeadlineLibraryTaskCommand(int TaskId) : IRequest<LibraryTaskOperationResponse>;

public class CompleteNoDeadlineLibraryTaskCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryTaskRepository taskRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CompleteNoDeadlineLibraryTaskCommand, LibraryTaskOperationResponse>
{
    public async Task<LibraryTaskOperationResponse> Handle(
        CompleteNoDeadlineLibraryTaskCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(
            currentUser.UserId.Value,
            cancellationToken);
        if (membership is null)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "You must be in a crew." };
        }

        var task = await taskRepository.GetTrackedTaskByIdAsync(request.TaskId, cancellationToken);
        if (task is null || task.CrewId != membership.CrewId || task.IsClosed)
        {
            return new LibraryTaskOperationResponse { Success = false, Message = "Quest not found." };
        }

        if (task.HasDeadline)
        {
            return new LibraryTaskOperationResponse
            {
                Success = false,
                Message = "This endpoint is only for no-deadline quests."
            };
        }

        if (task.CreatorUserId == currentUser.UserId.Value)
        {
            return new LibraryTaskOperationResponse
            {
                Success = false,
                Message = "You cannot complete your own quest."
            };
        }

        if (task.Instances.Any(i =>
                i.Status == LibraryTaskInstanceStatus.AwaitingConfirmation
                && i.ClaimedByUserId == currentUser.UserId.Value))
        {
            return new LibraryTaskOperationResponse
            {
                Success = false,
                Message = "Your completion is already awaiting confirmation."
            };
        }

        if (task.DeleteOnCompletion
            && task.Instances.Any(i => i.Status == LibraryTaskInstanceStatus.AwaitingConfirmation))
        {
            return new LibraryTaskOperationResponse
            {
                Success = false,
                Message = "A completion is already awaiting confirmation."
            };
        }

        var utcNow = DateTime.UtcNow;
        await taskRepository.AddInstancesAsync(
            [
                new LibraryTaskInstance
                {
                    TaskId = task.Id,
                    ScheduledAt = utcNow,
                    Status = LibraryTaskInstanceStatus.AwaitingConfirmation,
                    ClaimedByUserId = currentUser.UserId.Value,
                    ClaimedAt = utcNow,
                    CompletedAt = utcNow
                }
            ],
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LibraryTaskOperationResponse
        {
            Success = true,
            Message = "Marked complete — waiting for confirmation.",
            TaskId = task.Id
        };
    }
}
