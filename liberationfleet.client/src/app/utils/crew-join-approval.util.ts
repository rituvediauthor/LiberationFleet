import { NotificationItem } from '../models/notification.model';

/** Matches server title in CrewJoinRequestProposalService. */
export const JOIN_REQUEST_APPROVED_TITLE = 'Join request approved';

export function isCrewJoinRequestApprovedNotification(notification: NotificationItem): boolean {
  if (notification.kind !== 'ProposalAccepted') {
    return false;
  }

  if (notification.title === JOIN_REQUEST_APPROVED_TITLE) {
    return true;
  }

  // ActionUrl is /app/crew for join approvals (not the proposals list).
  const actionPath = (notification.actionUrl ?? '').split('?')[0];
  if (actionPath === '/app/crew') {
    return true;
  }

  return /approved to join/i.test(notification.body ?? '');
}

export function isNewSeasonNotification(notification: NotificationItem): boolean {
  return notification.kind === 'NewSeason';
}
