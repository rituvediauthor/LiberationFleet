using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Notifications;

public static class NotificationCategoryMapper
{
    public static NotificationFilterCategory? ToFilterCategory(NotificationKind kind) => kind switch
    {
        NotificationKind.NewReply or NotificationKind.NewForumComment
            or NotificationKind.ForumPostLiked or NotificationKind.ForumCommentLiked
            or NotificationKind.NewFleetForumComment or NotificationKind.NewFleetReply
            or NotificationKind.FleetForumPostLiked or NotificationKind.FleetForumCommentLiked
            or NotificationKind.NewGiftComment or NotificationKind.NewGiftReply
            or NotificationKind.GiftEntryLiked or NotificationKind.GiftCommentLiked
            => NotificationFilterCategory.Comments,
        NotificationKind.Mention or NotificationKind.FleetMention => NotificationFilterCategory.Mentions,
        NotificationKind.NewProposal or NotificationKind.ProposalRejected or NotificationKind.ProposalAccepted
            or NotificationKind.NewFleetProposal or NotificationKind.FleetProposalAccepted or NotificationKind.FleetProposalRejected
            or NotificationKind.NewProposalReply or NotificationKind.NewFleetProposalReply
            => NotificationFilterCategory.Proposals,
        NotificationKind.NewRule or NotificationKind.RuleDeleted or NotificationKind.RuleEdited
            or NotificationKind.NewFleetRule or NotificationKind.FleetRuleDeleted or NotificationKind.FleetRuleEdited
            => NotificationFilterCategory.Rules,
        _ => null
    };

    public static bool MatchesCategory(NotificationKind kind, NotificationFilterCategory category) =>
        category == NotificationFilterCategory.All || ToFilterCategory(kind) == category;

    public static bool MatchesCategory(Notification notification, NotificationFilterCategory category)
    {
        if (category == NotificationFilterCategory.All)
        {
            return true;
        }

        if (IsProposalReplyNotification(notification))
        {
            return category == NotificationFilterCategory.Proposals;
        }

        if (category == NotificationFilterCategory.Comments
            && NotificationLegacySupport.IsLegacyProposalReplyKind(notification.Kind))
        {
            return false;
        }

        return ToFilterCategory(notification.Kind) == category;
    }

    private static bool IsProposalReplyNotification(Notification notification) =>
        NotificationLegacySupport.IsProposalReplyNotification(notification);

    public static IReadOnlyList<NotificationKind> GetKindsForCategory(NotificationFilterCategory category) => category switch
    {
        NotificationFilterCategory.Comments =>
        [
            NotificationKind.NewReply,
            NotificationKind.NewForumComment,
            NotificationKind.ForumPostLiked,
            NotificationKind.ForumCommentLiked,
            NotificationKind.NewFleetForumComment,
            NotificationKind.NewFleetReply,
            NotificationKind.FleetForumPostLiked,
            NotificationKind.FleetForumCommentLiked,
            NotificationKind.NewGiftComment,
            NotificationKind.NewGiftReply,
            NotificationKind.GiftEntryLiked,
            NotificationKind.GiftCommentLiked
        ],
        NotificationFilterCategory.Mentions =>
        [
            NotificationKind.Mention,
            NotificationKind.FleetMention
        ],
        NotificationFilterCategory.Proposals =>
        [
            NotificationKind.NewProposal,
            NotificationKind.ProposalRejected,
            NotificationKind.ProposalAccepted,
            NotificationKind.NewFleetProposal,
            NotificationKind.FleetProposalAccepted,
            NotificationKind.FleetProposalRejected,
            NotificationKind.NewProposalReply,
            NotificationKind.NewFleetProposalReply
        ],
        NotificationFilterCategory.Rules =>
        [
            NotificationKind.NewRule,
            NotificationKind.RuleDeleted,
            NotificationKind.RuleEdited,
            NotificationKind.NewFleetRule,
            NotificationKind.FleetRuleDeleted,
            NotificationKind.FleetRuleEdited
        ],
        _ => Array.Empty<NotificationKind>()
    };
}
