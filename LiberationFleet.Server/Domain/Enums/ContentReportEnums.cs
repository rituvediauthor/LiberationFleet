namespace LiberationFleet.Server.Domain.Enums;

public enum ContentReportReason
{
    ChildSexualExploitation = 1,
    NonConsensualIntimateImage = 2,
    ThreatsOfViolence = 3,
    OtherIllegal = 4,
    Harassment = 5,
    Spam = 6
}

public enum ContentReportTargetType
{
    ChatMessage = 1,
    ForumPost = 2,
    ForumComment = 3,
    ProposalComment = 4,
    UserProfile = 5,
    DirectMessage = 6,
    Proposal = 7
}

public enum ContentReportStatus
{
    Received = 1,
    QueuedForNcmec = 2,
    EscalatedToVendor = 3,
    Actioned = 4,
    Closed = 5
}
