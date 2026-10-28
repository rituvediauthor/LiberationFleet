import {
  giftCategoryLabel,
  hasUsableLibraryPreviewTitle,
  isLibraryPreviewCandidateEligible,
  pickFirstAllowedAdult,
  pickLibraryPreviewCarousel,
  pickRandomLibraryPreview,
  truncateDashboardPreview
} from './dashboard-preview.util';

describe('dashboard-preview.util', () => {
  describe('truncateDashboardPreview', () => {
    it('returns empty for blank input', () => {
      expect(truncateDashboardPreview('   ')).toBe('');
      expect(truncateDashboardPreview(null)).toBe('');
    });

    it('keeps short text unchanged', () => {
      expect(truncateDashboardPreview('hello world')).toBe('hello world');
    });

    it('truncates at 500 characters with ellipsis', () => {
      const long = 'a'.repeat(520);
      const truncated = truncateDashboardPreview(long);
      expect(truncated.length).toBe(500);
      expect(truncated.endsWith('...')).toBeTrue();
      expect(truncated.slice(0, 497)).toBe('a'.repeat(497));
    });
  });

  describe('pickFirstAllowedAdult', () => {
    it('skips adult items when shouldShow returns false', () => {
      const items = [
        { id: 1, adult: true },
        { id: 2, adult: true },
        { id: 3, adult: false }
      ];
      const picked = pickFirstAllowedAdult(
        items,
        i => i.adult,
        flag => !flag
      );
      expect(picked?.id).toBe(3);
    });

    it('returns null when every item is blocked', () => {
      const items = [{ adult: true }, { adult: true }];
      expect(pickFirstAllowedAdult(items, i => i.adult, () => false)).toBeNull();
    });
  });

  describe('pickRandomLibraryPreview', () => {
    it('returns null for an empty pool', () => {
      expect(pickRandomLibraryPreview([])).toBeNull();
    });

    it('picks from units and quests using the random source', () => {
      const unit = {
        type: 'unit' as const,
        unit: {
          unitId: 9,
          offeringId: 1,
          holderUserId: 1,
          holderUsername: 'Ada',
          title: 'Ladder',
          descriptionPreview: 'Tall',
          categories: []
        }
      };
      const quest = {
        type: 'quest' as const,
        task: {
          taskId: 4,
          title: 'Sweep',
          creatorUsername: 'Bo',
          creatorUserId: 2,
          value: 5,
          scheduleSummary: 'Weekly',
          hasDeadline: true
        }
      };
      const pool = [unit, quest];
      expect(pickRandomLibraryPreview(pool, () => 0)).toEqual(unit);
      expect(pickRandomLibraryPreview(pool, () => 0.99)).toEqual(quest);
    });
  });

  describe('library preview eligibility', () => {
    it('rejects blank and encrypted-quest placeholder titles', () => {
      expect(hasUsableLibraryPreviewTitle('')).toBeFalse();
      expect(hasUsableLibraryPreviewTitle('   ')).toBeFalse();
      expect(hasUsableLibraryPreviewTitle('Encrypted quest')).toBeFalse();
      expect(hasUsableLibraryPreviewTitle('Mow lawn')).toBeTrue();
    });

    it('keeps encrypted quests even before their titles decrypt', () => {
      const encryptedQuest = {
        type: 'quest' as const,
        task: {
          taskId: 8,
          title: '',
          creatorUsername: 'Kai',
          creatorUserId: 3,
          value: 10,
          scheduleSummary: 'Weekly',
          hasEncryptedContent: true
        }
      };
      const blankUnit = {
        type: 'unit' as const,
        unit: {
          unitId: 1,
          offeringId: 1,
          holderUserId: 1,
          holderUsername: 'Ada',
          title: '',
          descriptionPreview: '',
          categories: []
        }
      };
      expect(isLibraryPreviewCandidateEligible(encryptedQuest)).toBeTrue();
      expect(isLibraryPreviewCandidateEligible(blankUnit)).toBeFalse();
    });
  });

  describe('pickLibraryPreviewCarousel', () => {
    it('returns up to maxItems in shuffled order', () => {
      const pool = [1, 2, 3, 4, 5, 6].map(unitId => ({
        type: 'unit' as const,
        unit: {
          unitId,
          offeringId: unitId,
          holderUserId: 1,
          holderUsername: 'Ada',
          title: `Item ${unitId}`,
          descriptionPreview: '',
          categories: []
        }
      }));
      const picked = pickLibraryPreviewCarousel(pool, 5, () => 0);
      expect(picked.length).toBe(5);
      expect(new Set(picked.map(item => item.type === 'unit' ? item.unit.unitId : 0)).size).toBe(5);
    });
  });

  it('maps gift category labels', () => {
    expect(giftCategoryLabel('survivalThreshold')).toBe('Survival threshold');
    expect(giftCategoryLabel(null)).toBe('');
  });
});
