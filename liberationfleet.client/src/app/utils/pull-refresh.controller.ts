/**
 * Overscroll pull-to-refresh for scroll containers (discussion-list style).
 * Supports top edge, bottom edge, or both.
 */
export type PullRefreshEdge = 'top' | 'bottom';

export interface PullRefreshBindOptions {
  edges: readonly PullRefreshEdge[];
  isBusy: () => boolean;
  onRefresh: (edge: PullRefreshEdge) => void;
  /** Called whenever pullDistance / activeEdge change (for Angular CD). */
  onDistanceChange?: (distance: number, edge: PullRefreshEdge | null) => void;
  threshold?: number;
  maxDistance?: number;
}

export class PullRefreshController {
  pullDistance = 0;
  activeEdge: PullRefreshEdge | null = null;

  private el: HTMLElement | null = null;
  private pullStartY: number | null = null;
  private readonly threshold: number;
  private readonly maxDistance: number;
  private readonly edges: ReadonlySet<PullRefreshEdge>;
  private readonly isBusy: () => boolean;
  private readonly onRefresh: (edge: PullRefreshEdge) => void;
  private readonly onDistanceChange?: (distance: number, edge: PullRefreshEdge | null) => void;

  constructor(options: PullRefreshBindOptions) {
    this.edges = new Set(options.edges);
    this.isBusy = options.isBusy;
    this.onRefresh = options.onRefresh;
    this.onDistanceChange = options.onDistanceChange;
    this.threshold = options.threshold ?? 56;
    this.maxDistance = options.maxDistance ?? 88;
  }

  bind(el: HTMLElement | null): void {
    if (el === this.el) {
      return;
    }
    this.unbind();
    this.el = el;
    if (!el) {
      return;
    }
    el.addEventListener('scroll', this.onScroll, { passive: true });
    el.addEventListener('touchstart', this.onTouchStart, { passive: true });
    el.addEventListener('touchmove', this.onTouchMove, { passive: false });
    el.addEventListener('touchend', this.onTouchEnd, { passive: true });
    el.addEventListener('wheel', this.onWheel, { passive: false });
  }

  unbind(): void {
    if (!this.el) {
      return;
    }
    this.el.removeEventListener('scroll', this.onScroll);
    this.el.removeEventListener('touchstart', this.onTouchStart);
    this.el.removeEventListener('touchmove', this.onTouchMove);
    this.el.removeEventListener('touchend', this.onTouchEnd);
    this.el.removeEventListener('wheel', this.onWheel);
    this.el = null;
  }

  reset(): void {
    this.pullDistance = 0;
    this.activeEdge = null;
    this.pullStartY = null;
    this.onDistanceChange?.(0, null);
  }

  private setDistance(distance: number, edge: PullRefreshEdge | null): void {
    this.pullDistance = distance;
    this.activeEdge = edge;
    this.onDistanceChange?.(distance, edge);
  }

  private atTop(el: HTMLElement): boolean {
    return el.scrollTop <= 1;
  }

  private atBottom(el: HTMLElement): boolean {
    return el.scrollTop + el.clientHeight >= el.scrollHeight - 1;
  }

  private readonly onScroll = (): void => {
    const el = this.el;
    if (!el) {
      return;
    }
    if (this.activeEdge === 'top' && !this.atTop(el)) {
      this.setDistance(0, null);
    } else if (this.activeEdge === 'bottom' && !this.atBottom(el)) {
      this.setDistance(0, null);
    }
  };

  private readonly onTouchStart = (event: TouchEvent): void => {
    if (this.isBusy()) {
      this.pullStartY = null;
      return;
    }
    this.pullStartY = event.touches[0]?.clientY ?? null;
    this.setDistance(0, null);
  };

  private readonly onTouchMove = (event: TouchEvent): void => {
    if (this.pullStartY == null || this.isBusy() || !this.el) {
      return;
    }

    const currentY = event.touches[0]?.clientY ?? this.pullStartY;
    const delta = currentY - this.pullStartY;

    if (this.edges.has('top') && this.atTop(this.el) && delta > 0) {
      event.preventDefault();
      this.setDistance(Math.min(this.maxDistance, delta * 0.45), 'top');
      return;
    }

    if (this.edges.has('bottom') && this.atBottom(this.el) && delta < 0) {
      event.preventDefault();
      this.setDistance(Math.min(this.maxDistance, Math.abs(delta) * 0.45), 'bottom');
      return;
    }

    this.setDistance(0, null);
  };

  private readonly onTouchEnd = (): void => {
    if (this.pullDistance >= this.threshold && this.activeEdge) {
      const edge = this.activeEdge;
      this.pullStartY = null;
      this.onRefresh(edge);
      return;
    }
    this.reset();
  };

  private readonly onWheel = (event: WheelEvent): void => {
    const el = this.el;
    if (!el || this.isBusy()) {
      return;
    }

    if (this.edges.has('top') && this.atTop(el) && event.deltaY < 0) {
      event.preventDefault();
      const next = Math.min(this.maxDistance, this.pullDistance + Math.abs(event.deltaY) * 0.15);
      this.setDistance(next, 'top');
      if (next >= this.threshold) {
        this.onRefresh('top');
      }
      return;
    }

    if (this.edges.has('bottom') && this.atBottom(el) && event.deltaY > 0) {
      event.preventDefault();
      const next = Math.min(this.maxDistance, this.pullDistance + Math.abs(event.deltaY) * 0.15);
      this.setDistance(next, 'bottom');
      if (next >= this.threshold) {
        this.onRefresh('bottom');
      }
    }
  };
}
