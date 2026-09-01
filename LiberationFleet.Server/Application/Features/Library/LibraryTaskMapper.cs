using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library;

public static class LibraryTaskMapper
{
    public static void ApplyScheduleFields(LibraryTask task, UpsertLibraryTaskRequest request)
    {
        task.HasDeadline = request.HasDeadline;
        if (!request.HasDeadline)
        {
            task.DeleteOnCompletion = request.DeleteOnCompletion;
            task.IsRecurring = false;
            task.Frequency = LibraryTaskRecurrenceFrequency.None;
            task.TimeSpecific = false;
            task.SpecificTimeMinutes = null;
            task.IsSpaced = false;
            task.Interval = 1;
            task.DaySpecific = false;
            task.WeekDays = null;
            task.MonthDays = null;
            task.YearMonth = null;
            task.YearDay = null;
            task.OneShotDueAt = null;
            return;
        }

        task.DeleteOnCompletion = false;
        task.IsRecurring = request.IsRecurring;
        task.Frequency = ParseFrequency(request.Frequency, request.IsRecurring);
        task.TimeSpecific = request.TimeSpecific;
        task.SpecificTimeMinutes = request.TimeSpecific
            ? Math.Clamp(request.SpecificTimeMinutes ?? 0, 0, 23 * 60 + 59)
            : null;
        task.IsSpaced = request.IsSpaced;
        task.Interval = Math.Max(1, request.Interval <= 0 ? 1 : request.Interval);
        task.DaySpecific = request.DaySpecific;
        task.WeekDays = LibraryTaskScheduleService.JoinInts(request.WeekDays);
        task.MonthDays = LibraryTaskScheduleService.JoinInts(request.MonthDays);
        task.YearMonth = request.YearMonth is >= 1 and <= 12 ? request.YearMonth : null;
        task.YearDay = request.YearDay is >= 1 and <= 31 ? request.YearDay : null;
        task.OneShotDueAt = request.IsRecurring ? null : request.OneShotDueAt;
    }

    public static bool ScheduleEquals(LibraryTask task, UpsertLibraryTaskRequest request)
    {
        if (task.HasDeadline != request.HasDeadline)
        {
            return false;
        }

        if (!request.HasDeadline)
        {
            return task.DeleteOnCompletion == request.DeleteOnCompletion;
        }

        var frequency = ParseFrequency(request.Frequency, request.IsRecurring);
        var minutes = request.TimeSpecific
            ? Math.Clamp(request.SpecificTimeMinutes ?? 0, 0, 23 * 60 + 59)
            : (int?)null;
        var weekDays = LibraryTaskScheduleService.JoinInts(request.WeekDays);
        var monthDays = LibraryTaskScheduleService.JoinInts(request.MonthDays);
        var oneShot = request.IsRecurring ? null : request.OneShotDueAt;

        return task.IsRecurring == request.IsRecurring
            && task.Frequency == frequency
            && task.TimeSpecific == request.TimeSpecific
            && task.SpecificTimeMinutes == minutes
            && task.IsSpaced == request.IsSpaced
            && task.Interval == Math.Max(1, request.Interval <= 0 ? 1 : request.Interval)
            && task.DaySpecific == request.DaySpecific
            && string.Equals(task.WeekDays, weekDays, StringComparison.Ordinal)
            && string.Equals(task.MonthDays, monthDays, StringComparison.Ordinal)
            && task.YearMonth == (request.YearMonth is >= 1 and <= 12 ? request.YearMonth : null)
            && task.YearDay == (request.YearDay is >= 1 and <= 31 ? request.YearDay : null)
            && Nullable.Equals(task.OneShotDueAt, oneShot);
    }

    public static LibraryTaskRecurrenceFrequency ParseFrequency(string? frequency, bool isRecurring)
    {
        if (!isRecurring)
        {
            return LibraryTaskRecurrenceFrequency.None;
        }

        return Enum.TryParse<LibraryTaskRecurrenceFrequency>(frequency, true, out var parsed)
            && parsed != LibraryTaskRecurrenceFrequency.None
            ? parsed
            : LibraryTaskRecurrenceFrequency.Daily;
    }

    public static IReadOnlyList<LibraryTaskInstance> CreateInstances(
        LibraryTask task,
        DateTime utcNow)
    {
        var dates = LibraryTaskScheduleService.GenerateUpcomingInstances(task, utcNow);
        return dates.Select(date => new LibraryTaskInstance
        {
            TaskId = task.Id,
            ScheduledAt = date,
            Status = LibraryTaskInstanceStatus.Open
        }).ToList();
    }

    public static LibraryTaskListItemDto ToListItem(LibraryTask task, DateTime utcNow)
    {
        DateTime? next = null;
        if (task.HasDeadline)
        {
            next = task.Instances
                .Where(i => i.Status is LibraryTaskInstanceStatus.Open
                    or LibraryTaskInstanceStatus.Claimed
                    or LibraryTaskInstanceStatus.AwaitingConfirmation)
                .Where(i => i.ScheduledAt >= utcNow.AddDays(-1) || i.Status != LibraryTaskInstanceStatus.Open)
                .OrderBy(i => i.ScheduledAt)
                .Select(i => (DateTime?)i.ScheduledAt)
                .FirstOrDefault();

            next ??= task.Instances
                .Where(i => i.Status != LibraryTaskInstanceStatus.Cancelled
                    && i.Status != LibraryTaskInstanceStatus.Confirmed)
                .OrderBy(i => i.ScheduledAt)
                .Select(i => (DateTime?)i.ScheduledAt)
                .FirstOrDefault();
        }

        return new LibraryTaskListItemDto
        {
            TaskId = task.Id,
            Title = task.HasEncryptedContent ? string.Empty : task.Title,
            CreatorUsername = task.CreatorUser?.Username ?? "Crewmate",
            CreatorUserId = task.CreatorUserId,
            Value = task.Value,
            HasDeadline = task.HasDeadline,
            DeleteOnCompletion = task.DeleteOnCompletion,
            ScheduleSummary = LibraryTaskScheduleService.BuildScheduleSummary(task),
            NextDueAt = next,
            HasEncryptedContent = task.HasEncryptedContent
        };
    }

    public static LibraryTaskDetailDto ToDetail(LibraryTask task, int currentUserId, DateTime utcNow)
    {
        var isCreator = task.CreatorUserId == currentUserId;
        var activeInstances = task.Instances
            .Where(i => i.Status != LibraryTaskInstanceStatus.Cancelled
                && i.Status != LibraryTaskInstanceStatus.Confirmed)
            .Where(i =>
            {
                if (task.HasDeadline
                    && i.ScheduledAt < utcNow
                    && i.Status == LibraryTaskInstanceStatus.Open)
                {
                    return false;
                }

                if (i.Status == LibraryTaskInstanceStatus.AwaitingConfirmation)
                {
                    return isCreator || i.ClaimedByUserId == currentUserId;
                }

                return true;
            })
            .OrderBy(i => i.ScheduledAt)
            .ToList();

        var allPending = task.Instances
            .Where(i => i.Status == LibraryTaskInstanceStatus.AwaitingConfirmation)
            .ToList();
        var myPending = allPending.Any(i => i.ClaimedByUserId == currentUserId);
        var canCompleteAnytime = !task.HasDeadline
            && !isCreator
            && !myPending
            && (!task.DeleteOnCompletion || allPending.Count == 0);

        IReadOnlyList<LibraryTaskInstanceDto> instances;
        if (task.HasDeadline)
        {
            instances = activeInstances.Select(i => ToInstanceDto(i, currentUserId, isCreator)).ToList();
        }
        else if (isCreator)
        {
            instances = allPending
                .OrderBy(i => i.CompletedAt ?? i.ScheduledAt)
                .Select(i => ToInstanceDto(i, currentUserId, isCreator))
                .ToList();
        }
        else
        {
            instances = Array.Empty<LibraryTaskInstanceDto>();
        }

        return new LibraryTaskDetailDto
        {
            TaskId = task.Id,
            Title = task.HasEncryptedContent ? string.Empty : task.Title,
            Details = task.HasEncryptedContent ? string.Empty : task.Details,
            HasEncryptedContent = task.HasEncryptedContent,
            Value = task.Value,
            CreatorUserId = task.CreatorUserId,
            CreatorUsername = task.CreatorUser?.Username ?? "Crewmate",
            IsCreator = isCreator,
            HasDeadline = task.HasDeadline,
            DeleteOnCompletion = task.DeleteOnCompletion,
            CanCompleteAnytime = canCompleteAnytime,
            HasPendingConfirmation = isCreator && allPending.Count > 0,
            PendingConfirmationInstanceIds = isCreator
                ? allPending.OrderBy(i => i.CompletedAt ?? i.ScheduledAt).Select(i => i.Id).ToList()
                : Array.Empty<int>(),
            AwaitingConfirmationForCurrentUser = myPending,
            IsRecurring = task.IsRecurring,
            Frequency = task.Frequency.ToString(),
            TimeSpecific = task.TimeSpecific,
            SpecificTimeMinutes = task.SpecificTimeMinutes,
            IsSpaced = task.IsSpaced,
            Interval = task.Interval,
            DaySpecific = task.DaySpecific,
            WeekDays = LibraryTaskScheduleService.ParseInts(task.WeekDays),
            MonthDays = LibraryTaskScheduleService.ParseInts(task.MonthDays),
            YearMonth = task.YearMonth,
            YearDay = task.YearDay,
            OneShotDueAt = task.OneShotDueAt,
            ScheduleSummary = LibraryTaskScheduleService.BuildScheduleSummary(task),
            Instances = instances
        };
    }

    public static LibraryTaskInstanceDto ToInstanceDto(
        LibraryTaskInstance instance,
        int currentUserId,
        bool isCreator)
    {
        var claimedByMe = instance.ClaimedByUserId == currentUserId;
        var selectable = instance.Status switch
        {
            LibraryTaskInstanceStatus.Open => true,
            LibraryTaskInstanceStatus.Claimed => claimedByMe,
            LibraryTaskInstanceStatus.AwaitingConfirmation => isCreator,
            _ => false
        };

        return new LibraryTaskInstanceDto
        {
            InstanceId = instance.Id,
            ScheduledAt = instance.ScheduledAt,
            CompletedAt = instance.CompletedAt,
            Status = instance.Status.ToString(),
            ClaimedByUserId = instance.ClaimedByUserId,
            ClaimedByUsername = instance.ClaimedByUser?.Username,
            ClaimedByCurrentUser = claimedByMe,
            Selectable = selectable
        };
    }

    public static LibraryCreatorContributionGiftDto ToGiftDto(CreatorContributionGiftDetails details) =>
        new()
        {
            GiftId = details.GiftId,
            ContributorUserId = details.ContributorUserId,
            ContributorUsername = details.ContributorUsername,
            Amount = details.Amount,
            ItemTitle = details.ItemTitle,
            RecipientUserId = details.RecipientUserId,
            RecipientUsername = details.RecipientUsername,
            CrewGiftRecipientUserId = details.CrewGiftRecipientUserId
        };
}
