export interface IdentityGroupOption {
  key: string;
  label: string;
}

/** Stable keys matching server IdentityGroupKeys. */
export const IDENTITY_GROUP_OPTIONS: IdentityGroupOption[] = [
  { key: 'NonWhite', label: 'Non-white' },
  { key: 'Woman', label: 'Woman' },
  { key: 'Lgbtqia', label: 'LGBTQIA+' },
  { key: 'NotConventionallyAttractive', label: 'Not conventionally attractive' },
  { key: 'Homeless', label: 'Homeless' },
  { key: 'VisiblyOrAudiblyDisabled', label: 'Visibly or audibly disabled' }
];

export function normalizeIdentityGroups(keys: string[] | null | undefined): string[] {
  if (!keys?.length) {
    return [];
  }

  const allowed = new Set(IDENTITY_GROUP_OPTIONS.map(o => o.key));
  const unique = new Set<string>();
  for (const key of keys) {
    if (allowed.has(key)) {
      unique.add(key);
    }
  }

  return IDENTITY_GROUP_OPTIONS.map(o => o.key).filter(k => unique.has(k));
}

export function toggleIdentityGroup(selected: string[], key: string, checked: boolean): string[] {
  const next = new Set(normalizeIdentityGroups(selected));
  if (checked) {
    next.add(key);
  } else {
    next.delete(key);
  }
  return normalizeIdentityGroups([...next]);
}
