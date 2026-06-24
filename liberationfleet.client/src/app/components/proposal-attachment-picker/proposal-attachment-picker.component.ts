import { ChangeDetectorRef, Component, Input, OnDestroy, inject } from '@angular/core';
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

  audioRecorder = new AudioRecorderController();

  private proposalCrypto = inject(ProposalCryptoService);
  private toastService = inject(ToastService);
  private cdr = inject(ChangeDetectorRef);

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
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const files = input.files;
    if (!files) {
      return;
    }

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
    input.value = '';
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

  private addAudioAttachment(blob: Blob) {
    this.attachments.push({
      blob,
      type: 'audio',
      resourceId: this.proposalCrypto.createResourceId(),
      previewUrl: URL.createObjectURL(blob)
    });
  }

  removeAttachment(index: number) {
    const attachment = this.attachments[index];
    if (attachment.previewUrl?.startsWith('blob:')) {
      URL.revokeObjectURL(attachment.previewUrl);
    }
    this.attachments.splice(index, 1);
  }

  attachmentLabel(attachment: PendingAttachment): string {
    if (attachment.file?.name) {
      return attachment.file.name;
    }
    return `${attachment.type} attachment`;
  }
}
