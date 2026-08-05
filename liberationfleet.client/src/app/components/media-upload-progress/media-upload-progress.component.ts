import { Component, inject } from '@angular/core';
import { CommonModule, AsyncPipe } from '@angular/common';
import { MediaUploadQueueService } from '../../services/media-upload-queue.service';

@Component({
  selector: 'app-media-upload-progress',
  standalone: true,
  imports: [CommonModule, AsyncPipe],
  templateUrl: './media-upload-progress.component.html',
  styleUrl: './media-upload-progress.component.css'
})
export class MediaUploadProgressComponent {
  private uploadQueue = inject(MediaUploadQueueService);

  readonly activeJobs$ = this.uploadQueue.activeJobs$;
  readonly hasActive$ = this.uploadQueue.hasActive$;
  readonly aggregateProgress$ = this.uploadQueue.aggregateProgress$;

  phaseLabel(phase: string): string {
    switch (phase) {
      case 'queued':
        return 'Queued';
      case 'encrypting':
        return 'Encrypting';
      case 'uploading':
        return 'Uploading';
      case 'finalizing':
        return 'Finishing';
      case 'error':
        return 'Failed';
      default:
        return 'Working';
    }
  }
}
