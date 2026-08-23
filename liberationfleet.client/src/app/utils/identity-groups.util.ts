export interface IdentityGroupOption {
  key: string;
  label: string;
}

/** Stable keys matching server IdentityGroupKeys. */
export const IDENTITY_GROUP_OPTIONS: IdentityGroupOption[] = [
  { key: 'PhysicallyDisfigured', label: 'Physically disfigured' },
  { key: 'PhysicallyDisabledOrUnaccommodated', label: 'Physically disabled or unaccommodated' },
  { key: 'CognitivelyDisabledOrUnaccommodated', label: 'Cognitively disabled or unaccommodated' },
  { key: 'Bipoc', label: 'BIPOC / person of color' },
  { key: 'Woman', label: 'Woman/femme' },
  { key: 'NotHeterosexual', label: 'Not heterosexual' },
  { key: 'Trans', label: 'Trans' },
  { key: 'Intersex', label: 'Intersex' },
  { key: 'UnhousedOrHousingInsecure', label: 'Unhoused / housing insecure' },
  { key: 'ImmigrantOrRefugee', label: 'Immigrant/refugee' },
  { key: 'ReligiousOrAreligiousMinority', label: 'Religious/a-religious minority' },
  { key: 'Neurodivergent', label: 'Neurodivergent' },
  { key: 'VisiblyOrAudiblyDisabled', label: 'Visibly or audibly disabled' },
  { key: 'OtherTargetedMinority', label: 'Other targeted minority' }
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
