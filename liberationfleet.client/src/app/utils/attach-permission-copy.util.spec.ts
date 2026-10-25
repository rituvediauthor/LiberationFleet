import {
  attachmentUnmetReason,
  formatAttachmentAmount,
  formatAttachmentTenure
} from './attach-permission-copy.util';

describe('attach-permission-copy', () => {
  it('formats tenure and money', () => {
    expect(formatAttachmentTenure(1)).toBe('1 day');
    expect(formatAttachmentTenure(30)).toBe('30 days');
    expect(formatAttachmentAmount(20)).toBe('$20');
    expect(formatAttachmentAmount(20.5)).toBe('$20.50');
  });

  it('returns the blocked and disabled messages', () => {
    expect(attachmentUnmetReason('blocked', { scope: 'crew', tenureDays: 30, minContribution: 20 }))
      .toBe('A moderator blocked your ability to attach files.');
    expect(attachmentUnmetReason('disabled', { scope: 'crew', tenureDays: 30, minContribution: 20 }))
      .toBe('File attachments are disabled, request an override from a moderator.');
  });

  it('explains tenure and contribution requirements', () => {
    expect(attachmentUnmetReason('requirements', { scope: 'crew', tenureDays: 30, minContribution: 20 }))
      .toBe('You need to have been a crewmate for 30 days and have contributed at least $20 in order to attach a file. Or you can request an override from a moderator.');
    expect(attachmentUnmetReason('requirements', { scope: 'fleet', tenureDays: 1, minContribution: 20.5 }))
      .toBe('You need to have been a fleet member for 1 day and have contributed at least $20.50 in order to attach a file. Or you can request an override from a moderator.');
  });
});
