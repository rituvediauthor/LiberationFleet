export type AttachmentBlockReason = 'disabled' | 'requirements' | 'blocked';

export function formatAttachmentTenure(days: number): string {
  const value = Number.isFinite(days) ? Math.max(0, Math.round(days)) : 0;
  return `${value} day${value === 1 ? '' : 's'}`;
}

export function formatAttachmentAmount(amount: number): string {
  const value = Number(amount);
  if (!Number.isFinite(value)) {
    return '$0';
  }
  return value % 1 === 0 ? `$${value}` : `$${value.toFixed(2)}`;
}

export function attachmentUnmetReason(
  reason: AttachmentBlockReason | string | null | undefined,
  options: { scope: 'crew' | 'fleet'; tenureDays: number; minContribution: number }
): string {
  if (reason === 'blocked') {
    return 'A moderator blocked your ability to attach files.';
  }

  if (reason === 'disabled') {
    return 'File attachments are disabled, request an override from a moderator.';
  }

  const tenure = formatAttachmentTenure(options.tenureDays);
  const amount = formatAttachmentAmount(options.minContribution);
  const member = options.scope === 'fleet' ? 'a fleet member' : 'a crewmate';
  return `You need to have been ${member} for ${tenure} and have contributed at least ${amount} in order to attach a file. Or you can request an override from a moderator.`;
}
