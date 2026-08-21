import { IDENTITY_GROUP_OPTIONS, normalizeIdentityGroups } from './identity-groups.util';

describe('identity-groups.util', () => {
  it('includes all server identity group keys with updated labels', () => {
    const keys = IDENTITY_GROUP_OPTIONS.map(option => option.key);
    expect(keys).toContain('NonWhite');
    expect(keys).toContain('Indigenous');
    expect(keys).toContain('TransOrNonbinary');
    expect(keys).toContain('PrimaryCaregiver');
    expect(IDENTITY_GROUP_OPTIONS.find(option => option.key === 'NonWhite')?.label)
      .toBe('BIPOC / person of color');
  });

  it('normalizes unknown keys and preserves order', () => {
    expect(normalizeIdentityGroups(['Woman', 'Unknown', 'Lgbtqia', 'Woman']))
      .toEqual(['Woman', 'Lgbtqia']);
  });
});
