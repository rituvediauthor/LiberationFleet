import { ChangeDetectorRef, Component, EventEmitter, Input, OnDestroy, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PendingAttachment } from '../../models/proposal.model';
import { ProposalCryptoService } from '../../services/crypto/proposal-crypto.service';
import { ToastService } from '../toast/toast.component';
import { AudioRecorderController } from '../../utils/audio-recorder.util';
import { compressMediaFile } from '../../utils/media-compression.util';
import {
  AttachmentMediaKind,
  defaultAcceptAttribute,
  MAX_AUDIO_BYTES,
  validateAttachmentFile
} from '../../utils/media-attachment-allowlist.util';

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
  /** @deprecated Prefer allowedKinds; still honored if set for the file dialog hint. */
  @Input() acceptTypes?: string;
  @Output() fileDialogOpenChange = new EventEmitter<boolean>();
  @Output() attachmentsChange = new EventEmitter<void>();

  audioRecorder = new AudioRecorderController();

  private proposalCrypto = inject(ProposalCryptoService);
  private toastService = inject(ToastService);
  private cdr = inject(ChangeDetectorRef);
  private fileDialogOpen = false;
  private windowFocusListener?: () => void;

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

  ngOnDestroy() {
    this.audioRecorder.cancel();
    this.clearWindowFocusListener();
  }

  onFileInputClick() {
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

      try {
        const compressed = result.kind === 'audio'
          ? file
          : await compressMediaFile(file, result.kind);
        this.attachments.push({
          file: compressed,
          type: result.kind,
          resourceId: this.proposalCrypto.createResourceId(),
          previewUrl: URL.createObjectURL(compressed)
        });
      } catch (error) {
        const message = error instanceof Error ? error.message : 'Failed to process attachment';
        this.toastService.error(message);
      }
    }

    this.cdr.markForCheck();
    this.attachmentsChange.emit();
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

  removeAttachment(index: number) {
    const attachment = this.attachments[index];
    if (attachment.previewUrl?.startsWith('blob:')) {
      URL.revokeObjectURL(attachment.previewUrl);
    }
    this.attachments.splice(index, 1);
    this.cdr.markForCheck();
    this.attachmentsChange.emit();
  }

  attachmentLabel(attachment: PendingAttachment): string {
    if (attachment.file?.name) {
      return attachment.file.name;
    }
    return `${attachment.type} attachment`;
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

    this.attachments.push({
      blob,
      type: 'audio',
      resourceId: this.proposalCrypto.createResourceId(),
      previewUrl: URL.createObjectURL(blob)
    });
    this.cdr.markForCheck();
    this.attachmentsChange.emit();
  }
}
