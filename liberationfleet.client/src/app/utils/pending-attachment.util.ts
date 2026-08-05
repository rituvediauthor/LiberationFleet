import { PendingAttachment } from '../models/proposal.model';

export function isPendingAttachmentBusy(attachment: PendingAttachment): boolean {
  return attachment.status === 'processing';
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
