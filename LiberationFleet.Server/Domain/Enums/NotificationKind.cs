namespace LiberationFleet.Server.Domain.Enums;

public enum NotificationKind
{
    NewProposal = 1,
    ProposalRejected = 2,
    ProposalAccepted = 3,
    NewGifts = 4,
    NewCycle = 5,
    NewSeason = 6,
    NewChatMessage = 7,
    NewReply = 8,
    NewForumPost = 9,
    NewProjectPost = 10,
    NewForumComment = 11,
    NewProjectComment = 12,
    NewCrewmate = 13,
    JoinRequestFromPerson = 14,
    JoinRequestFromCrew = 15,
    NewRule = 16,
    RuleDeleted = 17,
    RuleEdited = 18,
    CrewSettingChanged = 19,
    CrewmateKicked = 20,
    Mention = 21,
    CrewmateRejoinAllowed = 22,
    NewLibraryRequest = 23,
    LibraryRequestDenied = 24,
    LibraryRequestCompleted = 25,
    NewLibraryRequestMessage = 26,
    LibraryUnitBrokenReported = 27,
    LibraryUnitBrokenConfirmed = 28,
    LibraryUnitReportedFixed = 29
}
