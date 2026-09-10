import { NotificationItem } from '../models/notification.model';

/** Matches server title in CrewJoinRequestProposalService. */
export const JOIN_REQUEST_APPROVED_TITLE = 'Join request approved';

/** ProposalAccepted = 3, NewSeason = 6 in Domain.Enums.NotificationKind. */
const PROPOSAL_ACCEPTED_KIND_VALUES: ReadonlySet<string | number> = new Set([
  'ProposalAccepted',
  3,
  '3'
]);

const NEW_SEASON_KIND_VALUES: ReadonlySet<string | number> = new Set([
  'NewSeason',
  6,
  '6'
]);

function kindMatches(kind: string | number | undefined | null, allowed: ReadonlySet<string | number>): boolean {
  if (kind === undefined || kind === null) {
    return false;
  }

  return allowed.has(kind);
}

export function isCrewJoinRequestApprovedNotification(notification: NotificationItem): boolean {
  // Prefer title / actionUrl / body: SignalR historically sent Kind as a number,
  // and ProposalAccepted is shared with ordinary proposal acceptance.
  if (notification.title === JOIN_REQUEST_APPROVED_TITLE) {
    return true;
  }

  const actionPath = (notification.actionUrl ?? '').split('?')[0];
  if (actionPath === '/app/crew' && kindMatches(notification.kind, PROPOSAL_ACCEPTED_KIND_VALUES)) {
    return true;
  }

  if (/approved to join/i.test(notification.body ?? '')
    && kindMatches(notification.kind, PROPOSAL_ACCEPTED_KIND_VALUES)) {
    return true;
  }

  return false;
}

export function isNewSeasonNotification(notification: NotificationItem): boolean {
  return kindMatches(notification.kind, NEW_SEASON_KIND_VALUES);
}
