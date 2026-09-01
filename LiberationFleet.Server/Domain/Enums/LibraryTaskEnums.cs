namespace LiberationFleet.Server.Domain.Enums;

public enum LibraryTaskRecurrenceFrequency
{
    None = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Yearly = 4
}

public enum LibraryTaskInstanceStatus
{
    Open = 0,
    Claimed = 1,
    AwaitingConfirmation = 2,
    Confirmed = 3,
    Cancelled = 4
}
