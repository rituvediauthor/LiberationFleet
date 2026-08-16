import { PendingAttachment } from '../models/proposal.model';

export function isPendingAttachmentBusy(attachment: PendingAttachment): boolean {
  return attachment.status === 'processing' || attachment.status === 'uploading';
}

export function isPendingAttachmentReady(attachment: PendingAttachment): boolean {
  return attachment.status !== 'processing' && attachment.status !== 'error';
}

/** True when any attachment is still compressing/preparing. */
export function hasBusyPendingAttachments(attachments: PendingAttachment[]): boolean {
  return attachments.some(isPendingAttachmentBusy);
}

export function hasFailedPendingAttachments(attachments: PendingAttachment[]): boolean {
  return attachments.some(attachment => attachment.status === 'error');
}

/** Submit/send allowed only when every attachment is ready (or there are none). */
export function pendingAttachmentsAllowSubmit(attachments: PendingAttachment[]): boolean {
  return !hasBusyPendingAttachments(attachments) && !hasFailedPendingAttachments(attachments);
}

/**
 * Wait until every attachment finishes processing/uploading.
 * Rejects if any attachment ends in error or the wait times out.
 */
export async function waitForPendingAttachmentsReady(
  attachments: PendingAttachment[],
  options?: { timeoutMs?: number; pollMs?: number }
): Promise<void> {
  if (!attachments.length) {
    return;
  }

  const timeoutMs = options?.timeoutMs ?? 10 * 60 * 1000;
  const pollMs = options?.pollMs ?? 200;
  const started = Date.now();

  while (hasBusyPendingAttachments(attachments)) {
    await Promise.all(
      attachments
        .map(attachment => attachment.uploadTask)
        .filter((task): task is Promise<void> => !!task)
        .map(task => task.catch(() => undefined))
    );

    if (!hasBusyPendingAttachments(attachments)) {
      break;
    }

    if (Date.now() - started > timeoutMs) {
      throw new Error('Attachment upload timed out.');
    }

    await new Promise<void>(resolve => setTimeout(resolve, pollMs));
  }

  if (hasFailedPendingAttachments(attachments)) {
    const failed = attachments.find(attachment => attachment.status === 'error');
    throw new Error(failed?.progressLabel || 'Attachment upload failed.');
  }
}
