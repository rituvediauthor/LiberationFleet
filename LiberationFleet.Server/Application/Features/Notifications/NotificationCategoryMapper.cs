using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Notifications;

public static class NotificationCategoryMapper
{
    public static NotificationFilterCategory? ToFilterCategory(NotificationKind kind) => kind switch
    {
        NotificationKind.NewReply or NotificationKind.NewForumComment
            => NotificationFilterCategory.Comments,
        NotificationKind.Mention => NotificationFilterCategory.Mentions,
        NotificationKind.NewProposal or NotificationKind.ProposalRejected or NotificationKind.ProposalAccepted
            => NotificationFilterCategory.Proposals,
        NotificationKind.NewRule or NotificationKind.RuleDeleted or NotificationKind.RuleEdited
            => NotificationFilterCategory.Rules,
        _ => null
    };

    public static bool MatchesCategory(NotificationKind kind, NotificationFilterCategory category) =>
        category == NotificationFilterCategory.All || ToFilterCategory(kind) == category;

    public static IReadOnlyList<NotificationKind> GetKindsForCategory(NotificationFilterCategory category) => category switch
    {
        NotificationFilterCategory.Comments =>
        [
            NotificationKind.NewReply,
            NotificationKind.NewForumComment
        ],
        NotificationFilterCategory.Mentions => [NotificationKind.Mention],
        NotificationFilterCategory.Proposals =>
        [
            NotificationKind.NewProposal,
            NotificationKind.ProposalRejected,
            NotificationKind.ProposalAccepted
        ],
        NotificationFilterCategory.Rules =>
        [
            NotificationKind.NewRule,
            NotificationKind.RuleDeleted,
            NotificationKind.RuleEdited
        ],
        _ => Array.Empty<NotificationKind>()
    };
}
