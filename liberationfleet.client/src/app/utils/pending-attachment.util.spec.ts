import {
  hasBusyPendingAttachments,
  pendingAttachmentsAllowSubmit,
  waitForPendingAttachmentsReady
} from './pending-attachment.util';
import { PendingAttachment } from '../models/proposal.model';

describe('pending-attachment.util', () => {
  it('allows submit only when nothing is busy or failed', () => {
    const ready: PendingAttachment = {
      type: 'image',
      resourceId: 'a',
      status: 'ready',
      uploaded: true
    };
    expect(pendingAttachmentsAllowSubmit([ready])).toBeTrue();
    expect(pendingAttachmentsAllowSubmit([{ ...ready, status: 'uploading' }])).toBeFalse();
    expect(pendingAttachmentsAllowSubmit([{ ...ready, status: 'error' }])).toBeFalse();
  });

  it('waits for busy attachments then resolves', async () => {
    const attachment: PendingAttachment = {
      type: 'video',
      resourceId: 'v1',
      status: 'uploading',
      uploaded: false
    };

    const wait = waitForPendingAttachmentsReady([attachment], { pollMs: 20, timeoutMs: 1000 });
    setTimeout(() => {
      attachment.status = 'ready';
      attachment.uploaded = true;
    }, 40);

    await expectAsync(wait).toBeResolved();
    expect(hasBusyPendingAttachments([attachment])).toBeFalse();
  });

  it('rejects when an attachment fails', async () => {
    const attachment: PendingAttachment = {
      type: 'audio',
      resourceId: 'a1',
      status: 'error',
      progressLabel: 'Upload failed'
    };

    await expectAsync(waitForPendingAttachmentsReady([attachment])).toBeRejectedWithError(/Upload failed/);
  });
});
