import { ChangeDetectorRef, Component, EventEmitter, Input, OnDestroy, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PendingAttachment } from '../../models/proposal.model';
import { ProposalCryptoScope, ProposalCryptoService } from '../../services/crypto/proposal-crypto.service';
import { ToastService } from '../toast/toast.component';
import { AudioRecorderController } from '../../utils/audio-recorder.util';
import { compressMediaFile, extractVideoPosterFrame, warmMediaAudioContext } from '../../utils/media-compression.util';
import {
  AttachmentMediaKind,
  defaultAcceptAttribute,
  MAX_AUDIO_BYTES,
  validateAttachmentFile
} from '../../utils/media-attachment-allowlist.util';
import { pendingAttachmentsAllowSubmit } from '../../utils/pending-attachment.util';

@Component({
  selector: 'app-proposal-attachment-picker',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './proposal-attachment-picker.component.html',
  styleUrl: './proposal-attachment-picker.component.css'
})
export class ProposalAttachmentPickerComponent implements OnDestroy {
  @Input() attachments: PendingAttachment[] = [];
  @Input() allowAudioRecording = true;
  /** Restrict which media kinds may be attached (library offerings use image-only). */
  @Input() allowedKinds: AttachmentMediaKind[] = ['image', 'video', 'audio'];
  /** Max number of attachments; omit or set 0 for unlimited. */
  @Input() maxAttachments = 0;
  /** When set, compressed files upload in the background immediately. */
  @Input() cryptoScope: ProposalCryptoScope | null = null;
  /** @deprecated Prefer allowedKinds; still honored if set for the file dialog hint. */
  @Input() acceptTypes?: string;
  @Output() fileDialogOpenChange = new EventEmitter<boolean>();
  @Output() attachmentsChange = new EventEmitter<void>();
  /** Emits whenever busy/ready state changes (for submit gating). */
  @Output() readinessChange = new EventEmitter<boolean>();

  audioRecorder = new AudioRecorderController();

  private proposalCrypto = inject(ProposalCryptoService);
  private toastService = inject(ToastService);
  private cdr = inject(ChangeDetectorRef);
  private fileDialogOpen = false;
  private windowFocusListener?: () => void;
  private abortControllers = new Map<string, AbortController>();

  constructor() {
    this.audioRecorder.onStateChange = () => this.cdr.markForCheck();
    this.audioRecorder.onRecordingComplete = blob => {
      if (blob) {
        this.addAudioAttachment(blob);
      }
    };
  }

  get resolvedAcceptTypes(): string {
    return this.acceptTypes?.trim() || defaultAcceptAttribute(this.allowedKinds);
  }

  get canRecordAudio(): boolean {
    return this.allowAudioRecording && this.allowedKinds.includes('audio');
  }

  get canAddMore(): boolean {
    return this.maxAttachments <= 0 || this.attachments.length < this.maxAttachments;
  }

  get attachmentsReady(): boolean {
    return pendingAttachmentsAllowSubmit(this.attachments);
  }

  ngOnDestroy() {
    this.audioRecorder.cancel();
    this.clearWindowFocusListener();
    for (const controller of this.abortControllers.values()) {
      controller.abort();
    }
    this.abortControllers.clear();
  }

  onAttachPointerDown(event: Event) {
    // Keep focus behavior, but unlock audio as early as possible in the gesture.
    event.preventDefault();
    void warmMediaAudioContext();
  }

  onFileInputClick() {
    // Unlock AudioContext in this user gesture so video+audio compression can run later.
    void warmMediaAudioContext();
    this.setFileDialogOpen(true);
    this.clearWindowFocusListener();
    this.windowFocusListener = () => {
      this.clearWindowFocusListener();
      setTimeout(() => this.setFileDialogOpen(false), 0);
    };
    window.addEventListener('focus', this.windowFocusListener);
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const files = input.files;

    try {
      if (files) {
        void this.addSelectedFiles(Array.from(files));
      }
    } finally {
      input.value = '';
      this.setFileDialogOpen(false);
    }
  }

  private async addSelectedFiles(files: File[]) {
    for (const file of files) {
      if (!this.canAddMore) {
        this.toastService.error(
          this.maxAttachments === 1
            ? 'Only one attachment is allowed.'
            : `You can attach at most ${this.maxAttachments} files.`
        );
        break;
      }

      const result = validateAttachmentFile(file, this.allowedKinds);
      if (!result.ok) {
        if (result.reason === 'too-large') {
          this.toastService.error(`${file.name} is too large for this attachment type.`);
        } else if (result.reason === 'blocked') {
          this.toastService.error(`${file.name} is not an allowed file type.`);
        } else {
          this.toastService.error(`Unsupported file type: ${file.name}`);
        }
        continue;
      }

      const resourceId = this.proposalCrypto.createResourceId();
      const controller = new AbortController();
      this.abortControllers.set(resourceId, controller);

      const pending: PendingAttachment = {
        type: result.kind,
        resourceId,
        fileName: file.name,
        status: 'processing',
        progress: 1,
        progressLabel: result.kind === 'video' ? 'Preparing video…' : 'Processing…',
        previewUrl: result.kind === 'image' ? URL.createObjectURL(file) : undefined,
        abort: () => controller.abort()
      };
      this.attachments.push(pending);
      this.emitChange();

      if (result.kind === 'video') {
        void this.refreshVideoThumbnail(pending, file, controller.signal);
      }

      try {
        const compressed = await compressMediaFile(file, result.kind, {
          signal: controller.signal,
          onProgress: (percent, label) => {
            pending.progress = percent;
            pending.progressLabel = label;
            this.cdr.markForCheck();
          }
        });

        if (controller.signal.aborted) {
          this.removeByResourceId(resourceId);
          continue;
        }

        if (pending.previewUrl?.startsWith('blob:')) {
          URL.revokeObjectURL(pending.previewUrl);
        }
        pending.file = compressed;
        pending.previewUrl = result.kind === 'image' || result.kind === 'video'
          ? URL.createObjectURL(compressed)
          : undefined;
        pending.abort = undefined;
        this.abortControllers.delete(resourceId);

        if (result.kind === 'video') {
          void this.refreshVideoThumbnail(pending, compressed, controller.signal);
        }

        if (this.cryptoScope && (this.cryptoScope.crewId || this.cryptoScope.fleetId)) {
          pending.status = 'uploading';
          pending.progress = 0;
          pending.progressLabel = 'Uploading…';
          this.emitChange();
          try {
            await this.proposalCrypto.uploadAttachmentInBackground(
              this.cryptoScope,
              pending,
              (percent, label) => {
                pending.progress = percent;
                pending.progressLabel = label;
                this.cdr.markForCheck();
              }
            );
          } catch (uploadError) {
            const message = uploadError instanceof Error ? uploadError.message : 'Upload failed';
            this.toastService.error(message);
          }
        } else {
          pending.status = 'ready';
          pending.progress = 100;
          pending.progressLabel = 'Ready';
        }
      } catch (error) {
        this.abortControllers.delete(resourceId);
        if (error instanceof DOMException && error.name === 'AbortError') {
          this.removeByResourceId(resourceId);
          continue;
        }
        pending.status = 'error';
        pending.progress = 0;
        pending.progressLabel = 'Failed';
        pending.abort = undefined;
        const message = error instanceof Error ? error.message : 'Failed to process attachment';
        this.toastService.error(message);
      }

      this.emitChange();
    }
  }

  onFileInputCancel() {
    this.setFileDialogOpen(false);
  }

  async startRecording() {
    if (!this.canRecordAudio) {
      return;
    }
    try {
      await this.audioRecorder.start();
    } catch {
      this.toastService.error('Microphone access is required to record audio.');
    }
  }

  async stopRecording() {
    await this.audioRecorder.stop();
  }

  cancelRecording() {
    this.audioRecorder.cancel();
  }

  cancelAttachment(index: number) {
    const attachment = this.attachments[index];
    if (!attachment) {
      return;
    }
    attachment.abort?.();
    const controller = this.abortControllers.get(attachment.resourceId);
    controller?.abort();
    this.abortControllers.delete(attachment.resourceId);
    this.removeAttachment(index);
  }

  removeAttachment(index: number) {
    const attachment = this.attachments[index];
    if (!attachment) {
      return;
    }
    if (attachment.status === 'processing') {
      attachment.abort?.();
      this.abortControllers.get(attachment.resourceId)?.abort();
      this.abortControllers.delete(attachment.resourceId);
    }
    this.revokePreviewUrls(attachment);
    this.attachments.splice(index, 1);
    this.emitChange();
  }

  /** Image uses previewUrl; video chips use a JPEG poster thumbnail. */
  chipPreviewUrl(attachment: PendingAttachment): string | undefined {
    if (attachment.type === 'video') {
      return attachment.thumbnailUrl;
    }
    if (attachment.type === 'image') {
      return attachment.previewUrl;
    }
    return undefined;
  }

  attachmentLabel(attachment: PendingAttachment): string {
    if (attachment.fileName) {
      return attachment.fileName;
    }
    if (attachment.file?.name) {
      return attachment.file.name;
    }
    return `${attachment.type} attachment`;
  }

  progressPercent(attachment: PendingAttachment): number {
    return Math.max(0, Math.min(100, attachment.progress ?? 0));
  }

  private removeByResourceId(resourceId: string) {
    const index = this.attachments.findIndex(item => item.resourceId === resourceId);
    if (index >= 0) {
      this.revokePreviewUrls(this.attachments[index]);
      this.attachments.splice(index, 1);
      this.emitChange();
    }
  }

  private revokePreviewUrls(attachment: PendingAttachment) {
    if (attachment.previewUrl?.startsWith('blob:')) {
      URL.revokeObjectURL(attachment.previewUrl);
    }
    if (attachment.thumbnailUrl?.startsWith('blob:')) {
      URL.revokeObjectURL(attachment.thumbnailUrl);
    }
    attachment.previewUrl = undefined;
    attachment.thumbnailUrl = undefined;
  }

  private async refreshVideoThumbnail(
    pending: PendingAttachment,
    source: File | Blob,
    signal?: AbortSignal
  ) {
    try {
      const poster = await extractVideoPosterFrame(source);
      if (signal?.aborted || !this.attachments.includes(pending)) {
        return;
      }
      if (pending.thumbnailUrl?.startsWith('blob:')) {
        URL.revokeObjectURL(pending.thumbnailUrl);
      }
      pending.thumbnailUrl = URL.createObjectURL(poster);
      this.cdr.markForCheck();
    } catch {
      // Keep film icon placeholder if the browser cannot decode a frame yet.
    }
  }

  private emitChange() {
    this.cdr.markForCheck();
    this.attachmentsChange.emit();
    this.readinessChange.emit(this.attachmentsReady);
  }

  private setFileDialogOpen(open: boolean) {
    if (this.fileDialogOpen === open) {
      return;
    }
    this.fileDialogOpen = open;
    if (!open) {
      this.clearWindowFocusListener();
    }
    this.fileDialogOpenChange.emit(open);
  }

  private clearWindowFocusListener() {
    if (this.windowFocusListener) {
      window.removeEventListener('focus', this.windowFocusListener);
      this.windowFocusListener = undefined;
    }
  }

  private addAudioAttachment(blob: Blob) {
    if (!this.canAddMore) {
      this.toastService.error(
        this.maxAttachments === 1
          ? 'Only one attachment is allowed.'
          : `You can attach at most ${this.maxAttachments} files.`
      );
      return;
    }
    if (!this.allowedKinds.includes('audio')) {
      this.toastService.error('Audio attachments are not allowed here.');
      return;
    }
    if (blob.size > MAX_AUDIO_BYTES) {
      this.toastService.error('Recording is too large.');
      return;
    }

    void this.compressAndAddAudio(blob);
  }

  private async compressAndAddAudio(blob: Blob) {
    const resourceId = this.proposalCrypto.createResourceId();
    const controller = new AbortController();
    this.abortControllers.set(resourceId, controller);
    const pending: PendingAttachment = {
      type: 'audio',
      resourceId,
      fileName: `recording-${Date.now()}.webm`,
      status: 'processing',
      progress: 5,
      progressLabel: 'Processing audio…',
      abort: () => controller.abort()
    };
    this.attachments.push(pending);
    this.emitChange();

    try {
      const raw = new File([blob], pending.fileName!, {
        type: blob.type || 'audio/webm',
        lastModified: Date.now()
      });
      const compressed = await compressMediaFile(raw, 'audio', {
        signal: controller.signal,
        onProgress: (percent, label) => {
          pending.progress = percent;
          pending.progressLabel = label;
          this.cdr.markForCheck();
        }
      });
      if (compressed.size > MAX_AUDIO_BYTES) {
        this.toastService.error('Recording is too large.');
        this.removeByResourceId(resourceId);
        return;
      }

      pending.file = compressed;
      pending.previewUrl = URL.createObjectURL(compressed);
      pending.abort = undefined;
      this.abortControllers.delete(resourceId);

      if (this.cryptoScope && (this.cryptoScope.crewId || this.cryptoScope.fleetId)) {
        pending.status = 'uploading';
        pending.progress = 0;
        pending.progressLabel = 'Uploading…';
        this.emitChange();
        try {
          await this.proposalCrypto.uploadAttachmentInBackground(
            this.cryptoScope,
            pending,
            (percent, label) => {
              pending.progress = percent;
              pending.progressLabel = label;
              this.cdr.markForCheck();
            }
          );
        } catch {
          this.toastService.error('Upload failed.');
        }
      } else {
        pending.status = 'ready';
        pending.progress = 100;
        pending.progressLabel = 'Ready';
      }
      this.emitChange();
    } catch (error) {
      this.abortControllers.delete(resourceId);
      if (error instanceof DOMException && error.name === 'AbortError') {
        this.removeByResourceId(resourceId);
        return;
      }
      pending.status = 'error';
      pending.progressLabel = 'Failed';
      this.toastService.error('Failed to process recording.');
      this.emitChange();
    }
  }
}
