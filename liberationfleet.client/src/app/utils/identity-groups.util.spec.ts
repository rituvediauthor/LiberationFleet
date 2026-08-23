import { IDENTITY_GROUP_OPTIONS, normalizeIdentityGroups } from './identity-groups.util';

describe('identity-groups.util', () => {
  it('includes all targeted minority group keys with labels', () => {
    const keys = IDENTITY_GROUP_OPTIONS.map(option => option.key);
    expect(keys).toEqual([
      'PhysicallyDisfigured',
      'PhysicallyDisabledOrUnaccommodated',
      'CognitivelyDisabledOrUnaccommodated',
      'Bipoc',
      'Woman',
      'NotHeterosexual',
      'Trans',
      'Intersex',
      'UnhousedOrHousingInsecure',
      'ImmigrantOrRefugee',
      'ReligiousOrAreligiousMinority',
      'Neurodivergent',
      'VisiblyOrAudiblyDisabled',
      'OtherTargetedMinority'
    ]);
    expect(IDENTITY_GROUP_OPTIONS.find(option => option.key === 'Bipoc')?.label)
      .toBe('BIPOC / person of color');
    expect(IDENTITY_GROUP_OPTIONS.find(option => option.key === 'Woman')?.label)
      .toBe('Woman/femme');
  });

  it('drops legacy and unknown keys while preserving catalog order', () => {
    expect(normalizeIdentityGroups([
      'Woman',
      'Unknown',
      'NonWhite',
      'Trans',
      'Woman',
      'Lgbtqia'
    ])).toEqual(['Woman', 'Trans']);
  });
});
