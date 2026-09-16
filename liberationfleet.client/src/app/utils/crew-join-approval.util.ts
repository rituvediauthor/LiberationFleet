import { NotificationItem } from '../models/notification.model';

/** Matches server title in CrewJoinRequestProposalService. */
export const JOIN_REQUEST_APPROVED_TITLE = 'Join request approved';

/** ProposalAccepted = 3, NewSeason = 6, CrewJoinSwitchOffer = 60 in Domain.Enums.NotificationKind. */
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

const CREW_JOIN_SWITCH_OFFER_KIND_VALUES: ReadonlySet<string | number> = new Set([
  'CrewJoinSwitchOffer',
  60,
  '60'
]);

/** SecondaryEntityId: null/0 = pending; 1 = stayed; 2 = switched (server CrewJoinSwitchOfferDecision). */
export const CREW_JOIN_SWITCH_OFFER_STAYED = 1;
export const CREW_JOIN_SWITCH_OFFER_SWITCHED = 2;

function kindMatches(kind: string | number | undefined | null, allowed: ReadonlySet<string | number>): boolean {
  if (kind === undefined || kind === null) {
    return false;
  }

  return allowed.has(kind);
}

export function isCrewJoinSwitchOfferNotification(notification: NotificationItem): boolean {
  if (kindMatches(notification.kind, CREW_JOIN_SWITCH_OFFER_KIND_VALUES)) {
    return true;
  }

  // Fallback if kind mapping is missing but body matches the offer copy.
  return notification.title === JOIN_REQUEST_APPROVED_TITLE
    && /switch crews or stay/i.test(notification.body ?? '');
}

export function isPendingCrewJoinSwitchOffer(notification: NotificationItem): boolean {
  if (!isCrewJoinSwitchOfferNotification(notification)) {
    return false;
  }

  const decision = notification.secondaryEntityId;
  return decision == null || decision === 0;
}

export function isCrewJoinRequestApprovedNotification(notification: NotificationItem): boolean {
  // Switch/Stay offers reuse the same title — do not treat them as an applied join.
  if (isCrewJoinSwitchOfferNotification(notification)) {
    return false;
  }

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
