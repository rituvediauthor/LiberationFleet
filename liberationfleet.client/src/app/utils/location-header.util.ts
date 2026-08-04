import { ActivatedRouteSnapshot, Data } from '@angular/router';

/** Bottom-nav parent tabs used in fixed location headers. */
export type ParentTab = 'crew' | 'fleet' | 'friends' | 'notifications' | 'profile';

export interface LocationHeaderInfo {
  parentTab: ParentTab;
  parentLabel: string;
  parentPath: string;
  pageLabel: string;
}

const PARENT_TAB_LABELS: Record<ParentTab, string> = {
  crew: 'Crew',
  fleet: 'Fleet',
  friends: 'Friends',
  notifications: 'Notifications',
  profile: 'Profile'
};

const PARENT_TAB_PATHS: Record<ParentTab, string> = {
  crew: '/app/crew',
  fleet: '/app/fleet',
  friends: '/app/friends',
  notifications: '/app/notifications',
  profile: '/app/profile'
};

export function isParentTab(value: unknown): value is ParentTab {
  return value === 'crew'
    || value === 'fleet'
    || value === 'friends'
    || value === 'notifications'
    || value === 'profile';
}

export function parentTabLabel(tab: ParentTab): string {
  return PARENT_TAB_LABELS[tab];
}

export function parentTabPath(tab: ParentTab): string {
  return PARENT_TAB_PATHS[tab];
}

/** Walk to the deepest activated route and resolve location header data. */
export function resolveLocationHeader(
  snapshot: ActivatedRouteSnapshot | null | undefined
): LocationHeaderInfo | null {
  let current = snapshot ?? null;
  let deepest: ActivatedRouteSnapshot | null = current;
  while (current) {
    deepest = current;
    current = current.firstChild;
  }

  if (!deepest) {
    return null;
  }

  return locationHeaderFromData(deepest.data);
}

export function locationHeaderFromData(data: Data | null | undefined): LocationHeaderInfo | null {
  if (!data) {
    return null;
  }

  const parentTab = data['parentTab'];
  const pageLabel = typeof data['locationHeader'] === 'string'
    ? data['locationHeader'].trim()
    : '';

  if (!isParentTab(parentTab) || !pageLabel) {
    return null;
  }

  return {
    parentTab,
    parentLabel: parentTabLabel(parentTab),
    parentPath: parentTabPath(parentTab),
    pageLabel
  };
}
