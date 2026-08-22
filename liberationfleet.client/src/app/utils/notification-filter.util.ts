import { NotificationFilterCategory, NotificationKind } from '../models/notification.model';

const COMMENT_KINDS = new Set<NotificationKind>([
  'NewReply',
  'NewForumComment',
  'ForumPostLiked',
  'ForumCommentLiked',
  'NewFleetForumComment',
  'NewFleetReply',
  'FleetForumPostLiked',
  'FleetForumCommentLiked',
  'NewGiftComment',
  'NewGiftReply',
  'GiftEntryLiked',
  'GiftCommentLiked'
]);

const MENTION_KINDS = new Set<NotificationKind>(['Mention', 'FleetMention']);

const PROPOSAL_KINDS = new Set<NotificationKind>([
  'NewProposal',
  'ProposalRejected',
  'ProposalAccepted',
  'NewFleetProposal',
  'FleetProposalAccepted',
  'FleetProposalRejected',
  'NewProposalReply',
  'NewFleetProposalReply'
]);

const RULE_KINDS = new Set<NotificationKind>([
  'NewRule',
  'RuleDeleted',
  'RuleEdited',
  'NewFleetRule',
  'FleetRuleDeleted',
  'FleetRuleEdited'
]);

/** Mirrors server NotificationCategoryMapper.MatchesCategory. */
export function NotificationCategoryMapperMatches(
  kind: NotificationKind,
  category: NotificationFilterCategory
): boolean {
  if (category === 'All') {
    return true;
  }

  switch (category) {
    case 'Comments':
      return COMMENT_KINDS.has(kind);
    case 'Mentions':
      return MENTION_KINDS.has(kind);
    case 'Proposals':
      return PROPOSAL_KINDS.has(kind);
    case 'Rules':
      return RULE_KINDS.has(kind);
    default:
      return false;
  }
}
