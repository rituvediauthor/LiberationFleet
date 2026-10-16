import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ChatDashboardPreview,
  CrewDashboardPreview,
  CrewDashboardPreviewOpenTarget,
  ForumDashboardPreview,
  GiftDashboardPreview,
  LibraryDashboardPreview,
  LibraryDashboardPreviewItem,
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
export class CrewDashboardPreviewComponent implements OnChanges {
  @Input() preview: CrewDashboardPreview | null = null;
  @Input() loading = false;
  @Input() crewId: number | null = null;
  @Output() open = new EventEmitter<CrewDashboardPreviewOpenTarget>();
  private touchStartX: number | null = null;
  activeLibraryIndex = 0;

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
    const item = this.activeLibraryItem;
    return item?.libraryKind === 'unit'
      ? item
      : null;
  }

  get libraryQuestPreview(): LibraryQuestDashboardPreview | null {
    const item = this.activeLibraryItem;
    return item?.libraryKind === 'quest'
      ? item
      : null;
  }

  get libraryPreview(): LibraryDashboardPreview | null {
    return this.preview?.kind === 'library' ? this.preview : null;
  }

  get activeLibraryItem(): LibraryDashboardPreviewItem | null {
    const items = this.libraryPreview?.items ?? [];
    if (!items.length) {
      return null;
    }
    const index = Math.min(this.activeLibraryIndex, items.length - 1);
    return items[index] ?? null;
  }

  get hasLibraryCarouselControls(): boolean {
    return (this.libraryPreview?.items.length ?? 0) > 1;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['preview']) {
      this.activeLibraryIndex = 0;
      this.touchStartX = null;
    }
  }

  formatTime(date: Date | string | null | undefined): string {
    return formatDashboardTimestamp(date);
  }

  onOpen(): void {
    if (this.preview?.kind === 'library') {
      const item = this.activeLibraryItem;
      if (item) {
        this.open.emit(item);
      }
      return;
    }
    if (this.preview) {
      this.open.emit(this.preview);
    }
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.onOpen();
    }
  }

  showPreviousLibraryItem(event: Event): void {
    event.stopPropagation();
    const items = this.libraryPreview?.items ?? [];
    if (items.length < 2) {
      return;
    }
    this.activeLibraryIndex = (this.activeLibraryIndex - 1 + items.length) % items.length;
  }

  showNextLibraryItem(event: Event): void {
    event.stopPropagation();
    const items = this.libraryPreview?.items ?? [];
    if (items.length < 2) {
      return;
    }
    this.activeLibraryIndex = (this.activeLibraryIndex + 1) % items.length;
  }

  onTouchStart(event: TouchEvent): void {
    this.touchStartX = event.touches[0]?.clientX ?? null;
  }

  onTouchEnd(event: TouchEvent): void {
    const startX = this.touchStartX;
    const endX = event.changedTouches[0]?.clientX ?? null;
    this.touchStartX = null;
    if (startX === null || endX === null || !this.hasLibraryCarouselControls) {
      return;
    }
    const deltaX = endX - startX;
    if (Math.abs(deltaX) < 36) {
      return;
    }
    if (deltaX < 0) {
      this.showNextLibraryItem(event);
    } else {
      this.showPreviousLibraryItem(event);
    }
  }
}
