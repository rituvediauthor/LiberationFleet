import { ChangeDetectorRef, Component, EventEmitter, Input, OnDestroy, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PendingAttachment } from '../../models/proposal.model';
import { ProposalCryptoService } from '../../services/crypto/proposal-crypto.service';
import { ToastService } from '../toast/toast.component';
import { AudioRecorderController } from '../../utils/audio-recorder.util';

@Component({
  selector: 'app-proposal-attachment-picker',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './proposal-attachment-picker.component.html',
  styleUrl: './proposal-attachment-picker.component.css'
})
export class ProposalAttachmentPickerComponent implements OnDestroy {
  @Input() attachments: PendingAttachment[] = [];
  @Output() fileDialogOpenChange = new EventEmitter<boolean>();

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
        Array.from(files).forEach(file => {
          const type = this.resolveAttachmentType(file);
          if (!type) {
            this.toastService.error(`Unsupported file type: ${file.name}`);
            return;
          }

          this.attachments.push({
            file,
            type,
            resourceId: this.proposalCrypto.createResourceId(),
            previewUrl: URL.createObjectURL(file)
          });
        });
      }
    } finally {
      input.value = '';
      this.setFileDialogOpen(false);
      this.cdr.markForCheck();
    }
  }

  onFileInputCancel() {
    this.setFileDialogOpen(false);
  }

  async startRecording() {
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

  private resolveAttachmentType(file: File): PendingAttachment['type'] | null {
    const mime = file.type.toLowerCase();
    if (mime.startsWith('image/')) {
      return 'image';
    }
    if (mime.startsWith('video/')) {
      return 'video';
    }
    if (mime.startsWith('audio/')) {
      return 'audio';
    }

    const extension = file.name.split('.').pop()?.toLowerCase();
    switch (extension) {
      case 'jpg':
      case 'jpeg':
      case 'png':
      case 'gif':
      case 'webp':
      case 'bmp':
      case 'heic':
        return 'image';
      case 'mp4':
      case 'mov':
      case 'webm':
      case 'mkv':
      case 'avi':
        return 'video';
      case 'mp3':
      case 'wav':
      case 'm4a':
      case 'ogg':
      case 'aac':
      case 'flac':
        return 'audio';
      default:
        return null;
    }
  }

  private addAudioAttachment(blob: Blob) {
    this.attachments.push({
      blob,
      type: 'audio',
      resourceId: this.proposalCrypto.createResourceId(),
      previewUrl: URL.createObjectURL(blob)
    });
    this.cdr.markForCheck();
  }
}
