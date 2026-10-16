import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ChatDashboardPreview,
  CrewDashboardPreview,
  ForumDashboardPreview,
  GiftDashboardPreview,
  LibraryQuestDashboardPreview,
  LibraryUnitDashboardPreview
} from '../../models/crew-dashboard-preview.model';
import { LibraryItemCardComponent } from '../library-item-card/library-item-card.component';
import { UserAvatarComponent } from '../user-avatar/user-avatar.component';
import { formatDashboardTimestamp } from '../../utils/dashboard-preview.util';

@Component({
  selector: 'app-crew-dashboard-preview',
  standalone: true,
  imports: [CommonModule, LibraryItemCardComponent, UserAvatarComponent],
  templateUrl: './crew-dashboard-preview.component.html',
  styleUrl: './crew-dashboard-preview.component.css'
})
export class CrewDashboardPreviewComponent {
  @Input() preview: CrewDashboardPreview | null = null;
  @Input() loading = false;
  @Input() crewId: number | null = null;
  @Output() open = new EventEmitter<CrewDashboardPreview>();

  get forumPreview(): ForumDashboardPreview | null {
    return this.preview?.kind === 'forum' ? this.preview : null;
  }

  get chatPreview(): ChatDashboardPreview | null {
    return this.preview?.kind === 'chat' ? this.preview : null;
  }

  get giftPreview(): GiftDashboardPreview | null {
    return this.preview?.kind === 'gift' ? this.preview : null;
  }

  get libraryUnitPreview(): LibraryUnitDashboardPreview | null {
    return this.preview?.kind === 'library' && this.preview.libraryKind === 'unit'
      ? this.preview
      : null;
  }

  get libraryQuestPreview(): LibraryQuestDashboardPreview | null {
    return this.preview?.kind === 'library' && this.preview.libraryKind === 'quest'
      ? this.preview
      : null;
  }

  formatTime(date: Date | string | null | undefined): string {
    return formatDashboardTimestamp(date);
  }

  onOpen(): void {
    if (this.preview) {
      this.open.emit(this.preview);
    }
  }
}
