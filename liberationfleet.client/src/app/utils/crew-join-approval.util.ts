import { NotificationItem } from '../models/notification.model';

/** Matches server title in CrewJoinRequestProposalService. */
export const JOIN_REQUEST_APPROVED_TITLE = 'Join request approved';

export function isCrewJoinRequestApprovedNotification(notification: NotificationItem): boolean {
  return notification.kind === 'ProposalAccepted'
    && notification.title === JOIN_REQUEST_APPROVED_TITLE;
}

/**
 * No-crew crew surfaces where the user should be taken to the dashboard
 * once a join request is approved.
 */
export function isCrewDashboardRedirectUrl(url: string): boolean {
  const path = url.split('?')[0].split('#')[0];
  return path === '/app/crew'
    || path.startsWith('/app/crew/join')
    || path.startsWith('/app/crew/create')
    || path.startsWith('/app/crew/invitations');
}
