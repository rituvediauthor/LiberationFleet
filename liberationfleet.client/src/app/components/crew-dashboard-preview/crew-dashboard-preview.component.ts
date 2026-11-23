import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  ViewChild,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ChatDashboardPreview,
  ChatDashboardPreviewItem,
  CrewDashboardPreview,
  CrewDashboardPreviewKind,
  CrewDashboardPreviewOpenTarget,
  ForumDashboardPreview,
  ForumDashboardPreviewItem,
  GiftDashboardPreview,
  GiftDashboardPreviewItem,
  LibraryDashboardPreview,
  LibraryDashboardPreviewItem,
  LibraryQuestDashboardPreview,
  LibraryUnitDashboardPreview
} from '../../models/crew-dashboard-preview.model';
import { LibraryItemCardComponent } from '../library-item-card/library-item-card.component';
import { AuthorIdentityComponent } from '../author-identity/author-identity.component';
import { formatDashboardTimestamp } from '../../utils/dashboard-preview.util';
import { preferSwipeCarousel } from '../../utils/haptic.util';
import { PreviewCarouselController } from '../../utils/preview-carousel.controller';
import { questValueCardNote } from '../../pages/library/library-tasks/library-task-copy';

@Component({
  selector: 'app-crew-dashboard-preview',
  standalone: true,
  imports: [CommonModule, LibraryItemCardComponent, AuthorIdentityComponent],
  templateUrl: './crew-dashboard-preview.component.html',
  styleUrl: './crew-dashboard-preview.component.css'
})
export class CrewDashboardPreviewComponent implements OnChanges, AfterViewInit, OnDestroy {
  @Input() preview: CrewDashboardPreview | null = null;
  @Input() loading = false;
  @Input() crewId: number | null = null;
  @Input() fleetId: number | null = null;
  @Input() linkScope: 'crew' | 'fleet' | 'auto' = 'auto';
  @Output() open = new EventEmitter<CrewDashboardPreviewOpenTarget>();
  @Output() needMore = new EventEmitter<CrewDashboardPreviewKind>();
  @ViewChild('viewport') viewport?: ElementRef<HTMLElement>;

  private readonly changeDetector = inject(ChangeDetectorRef);
  readonly carousel: PreviewCarouselController;
  private resizeObserver: ResizeObserver | null = null;
  private lastPreviewRef: CrewDashboardPreview | null = null;
  viewportWidth = 1;
  useSwipe = preferSwipeCarousel();

  constructor() {
    this.carousel = new PreviewCarouselController({
      preferSwipe: () => this.useSwipe,
      onIndexChange: () => this.changeDetector.markForCheck(),
      onNeedMore: () => {
        if (this.preview?.kind) {
          this.needMore.emit(this.preview.kind);
        }
      }
    });
  }

  get forumPreview(): ForumDashboardPreview | null {
    return this.preview?.kind === 'forum' ? this.preview : null;
  }

  get chatPreview(): ChatDashboardPreview | null {
    return this.preview?.kind === 'chat' ? this.preview : null;
  }

  get giftPreview(): GiftDashboardPreview | null {
    return this.preview?.kind === 'gift' ? this.preview : null;
  }

  get libraryPreview(): LibraryDashboardPreview | null {
    return this.preview?.kind === 'library' ? this.preview : null;
  }

  get items(): Array<
    ForumDashboardPreviewItem
    | ChatDashboardPreviewItem
    | GiftDashboardPreviewItem
    | LibraryDashboardPreviewItem
  > {
    return this.preview?.items ?? [];
  }

  get hasCarouselControls(): boolean {
    return this.items.length > 1;
  }

  get showDesktopArrows(): boolean {
    return this.hasCarouselControls && !this.useSwipe;
  }

  get trackTransform(): string {
    return this.carousel.trackTransform(this.viewportWidth);
  }

  get activeIndex(): number {
    return this.carousel.activeIndex;
  }

  ngAfterViewInit(): void {
    this.measureViewport();
    if (typeof ResizeObserver !== 'undefined' && this.viewport?.nativeElement) {
      this.resizeObserver = new ResizeObserver(() => this.measureViewport());
      this.resizeObserver.observe(this.viewport.nativeElement);
    }
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.resizeObserver = null;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['preview']) {
      return;
    }

    const previous = this.lastPreviewRef;
    const next = this.preview;
    this.lastPreviewRef = next;
    const previousCount = previous?.items?.length ?? 0;
    const nextCount = next?.items?.length ?? 0;
    const sameKind = previous?.kind === next?.kind;
    const appended = sameKind && nextCount > previousCount && previousCount > 0;

    this.useSwipe = preferSwipeCarousel();
    this.carousel.setItemCount(nextCount);

    if (!appended) {
      this.carousel.reset(0);
      this.carousel.setItemCount(nextCount);
    }

    // Defer measure so it does not trip ExpressionChangedAfterItHasBeenChecked in tests/dev.
    setTimeout(() => this.measureViewport(), 0);
  }

  formatTime(date: Date | string | null | undefined): string {
    return formatDashboardTimestamp(date);
  }

  questValueNote(value: number): string {
    return questValueCardNote(value);
  }

  asForum(item: unknown): ForumDashboardPreviewItem {
    return item as ForumDashboardPreviewItem;
  }

  asChat(item: unknown): ChatDashboardPreviewItem {
    return item as ChatDashboardPreviewItem;
  }

  asGift(item: unknown): GiftDashboardPreviewItem {
    return item as GiftDashboardPreviewItem;
  }

  asLibraryUnit(item: unknown): LibraryUnitDashboardPreview | null {
    const lib = item as LibraryDashboardPreviewItem;
    return lib?.libraryKind === 'unit' ? lib : null;
  }

  asLibraryQuest(item: unknown): LibraryQuestDashboardPreview | null {
    const lib = item as LibraryDashboardPreviewItem;
    return lib?.libraryKind === 'quest' ? lib : null;
  }

  onOpen(item?: CrewDashboardPreviewOpenTarget): void {
    const target = item ?? this.items[this.activeIndex];
    if (target) {
      this.open.emit(target);
    }
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.onOpen();
    }
  }

  showPrevious(event: Event): void {
    event.stopPropagation();
    this.carousel.showPrevious();
  }

  showNext(event: Event): void {
    event.stopPropagation();
    this.carousel.showNext();
  }

  onTouchStart(event: TouchEvent): void {
    this.measureViewport();
    this.carousel.onTouchStart(event, this.viewportWidth);
  }

  onTouchMove(event: TouchEvent): void {
    this.carousel.onTouchMove(event);
  }

  onTouchEnd(): void {
    this.carousel.onTouchEnd();
  }

  private measureViewport(): void {
    const width = this.viewport?.nativeElement?.clientWidth ?? 0;
    if (width > 0) {
      this.viewportWidth = width;
      this.changeDetector.markForCheck();
    }
  }
}
