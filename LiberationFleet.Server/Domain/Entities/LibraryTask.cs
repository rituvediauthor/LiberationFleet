using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class LibraryTask
{
    public int Id { get; set; }
    public int CrewId { get; set; }
    public int CreatorUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public bool HasEncryptedContent { get; set; }
    public decimal Value { get; set; }
    /// <summary>When false, the task has no due date — do it when you can.</summary>
    public bool HasDeadline { get; set; } = true;
    /// <summary>
    /// When true (no-deadline only), the task is closed after a completion is confirmed.
    /// When false, the task stays open for any number of completions.
    /// </summary>
    public bool DeleteOnCompletion { get; set; }
    public bool IsRecurring { get; set; }
    public LibraryTaskRecurrenceFrequency Frequency { get; set; }
    public bool TimeSpecific { get; set; }
    /// <summary>Minutes from midnight (0–1439) when <see cref="TimeSpecific"/> is true.</summary>
    public int? SpecificTimeMinutes { get; set; }
    public bool IsSpaced { get; set; }
    /// <summary>Every N days/weeks/months/years when spaced; otherwise 1.</summary>
    public int Interval { get; set; } = 1;
    public bool DaySpecific { get; set; }
    /// <summary>Comma-separated days of week 0=Sunday … 6=Saturday.</summary>
    public string? WeekDays { get; set; }
    /// <summary>Comma-separated month days 1–31 (31 = day 31 or last day of month).</summary>
    public string? MonthDays { get; set; }
    /// <summary>1–12 for yearly month-specific recurrence.</summary>
    public int? YearMonth { get; set; }
    /// <summary>1–31 for yearly day (31 = end of month when month is shorter).</summary>
    public int? YearDay { get; set; }
    /// <summary>Due instant for one-shot (non-recurring) tasks.</summary>
    public DateTime? OneShotDueAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsClosed { get; set; }

    public Crew Crew { get; set; } = null!;
    public User CreatorUser { get; set; } = null!;
    public ICollection<LibraryTaskInstance> Instances { get; set; } = new List<LibraryTaskInstance>();
}

public class LibraryTaskInstance
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public LibraryTaskInstanceStatus Status { get; set; } = LibraryTaskInstanceStatus.Open;
    public int? ClaimedByUserId { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public int? ContributionGiftId { get; set; }

    public LibraryTask Task { get; set; } = null!;
    public User? ClaimedByUser { get; set; }
}
