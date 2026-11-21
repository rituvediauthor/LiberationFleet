/**
 * Swipe-to-next carousel (not free horizontal scroll).
 * Content follows the finger; release snaps to previous / current / next.
 * Swipe left (negative deltaX) → next; swipe right → previous.
 */
export interface PreviewCarouselCallbacks {
  onIndexChange: (index: number) => void;
  /** Fired when the user advances near the end so the host can lazy-load. */
  onNeedMore?: (index: number) => void;
  preferSwipe: () => boolean;
}

export class PreviewCarouselController {
  activeIndex = 0;
  dragOffsetPx = 0;
  isDragging = false;
  private itemCount = 0;
  private startX: number | null = null;
  private startY: number | null = null;
  private widthPx = 1;
  private lockedAxis: 'x' | 'y' | null = null;
  private readonly onIndexChange: (index: number) => void;
  private readonly onNeedMore?: (index: number) => void;
  private readonly preferSwipe: () => boolean;

  constructor(callbacks: PreviewCarouselCallbacks) {
    this.onIndexChange = callbacks.onIndexChange;
    this.onNeedMore = callbacks.onNeedMore;
    this.preferSwipe = callbacks.preferSwipe;
  }

  setItemCount(count: number): void {
    this.itemCount = Math.max(0, count);
    if (this.activeIndex >= this.itemCount && this.itemCount > 0) {
      this.activeIndex = this.itemCount - 1;
    }
    if (this.itemCount === 0) {
      this.activeIndex = 0;
    }
  }

  setActiveIndex(index: number, opts?: { silent?: boolean }): void {
    if (this.itemCount <= 0) {
      this.activeIndex = 0;
      return;
    }
    const next = Math.max(0, Math.min(this.itemCount - 1, index));
    if (next === this.activeIndex) {
      return;
    }
    this.activeIndex = next;
    if (!opts?.silent) {
      this.onIndexChange(next);
      this.maybeRequestMore(next);
    }
  }

  reset(index = 0): void {
    this.activeIndex = index;
    this.dragOffsetPx = 0;
    this.isDragging = false;
    this.startX = null;
    this.startY = null;
    this.lockedAxis = null;
  }

  trackTransform(widthPx: number): string {
    const w = Math.max(1, widthPx || this.widthPx);
    this.widthPx = w;
    // Percentage slide for the index; pixel offset while dragging so content follows the finger.
    const dragPct = (this.dragOffsetPx / w) * 100;
    return `translate3d(calc(${-this.activeIndex * 100}% + ${dragPct}%), 0, 0)`;
  }

  showNext(): void {
    if (this.activeIndex >= this.itemCount - 1) {
      this.maybeRequestMore(this.activeIndex);
      return;
    }
    this.setActiveIndex(this.activeIndex + 1);
  }

  showPrevious(): void {
    if (this.activeIndex <= 0) {
      return;
    }
    this.setActiveIndex(this.activeIndex - 1);
  }

  onTouchStart(event: TouchEvent, widthPx: number): void {
    if (!this.preferSwipe() || this.itemCount < 2) {
      return;
    }
    const touch = event.touches[0];
    if (!touch) {
      return;
    }
    this.widthPx = Math.max(1, widthPx);
    this.startX = touch.clientX;
    this.startY = touch.clientY;
    this.lockedAxis = null;
    this.isDragging = true;
    this.dragOffsetPx = 0;
  }

  onTouchMove(event: TouchEvent): void {
    if (!this.isDragging || this.startX == null || this.startY == null) {
      return;
    }
    const touch = event.touches[0];
    if (!touch) {
      return;
    }
    const dx = touch.clientX - this.startX;
    const dy = touch.clientY - this.startY;
    if (!this.lockedAxis) {
      if (Math.abs(dx) < 8 && Math.abs(dy) < 8) {
        return;
      }
      this.lockedAxis = Math.abs(dx) >= Math.abs(dy) ? 'x' : 'y';
    }
    if (this.lockedAxis !== 'x') {
      return;
    }
    event.preventDefault();
    // Rubber-band at ends (right at start / left at end).
    let offset = dx;
    if ((this.activeIndex <= 0 && dx > 0) || (this.activeIndex >= this.itemCount - 1 && dx < 0)) {
      offset = dx * 0.35;
    }
    this.dragOffsetPx = offset;
  }

  onTouchEnd(): void {
    if (!this.isDragging) {
      return;
    }
    const offset = this.dragOffsetPx;
    const threshold = Math.max(40, this.widthPx / 3);
    this.isDragging = false;
    this.startX = null;
    this.startY = null;
    this.lockedAxis = null;
    this.dragOffsetPx = 0;

    // Swipe left (negative) → next; swipe right (positive) → previous.
    if (offset <= -threshold) {
      this.showNext();
      return;
    }
    if (offset >= threshold) {
      this.showPrevious();
    }
  }

  private maybeRequestMore(index: number): void {
    if (!this.onNeedMore) {
      return;
    }
    // Prefetch when within one of the end so the next swipe rarely waits.
    if (index >= this.itemCount - 2) {
      this.onNeedMore(index);
    }
  }
}
