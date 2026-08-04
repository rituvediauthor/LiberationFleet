import {
  AfterViewInit,
  Directive,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  inject
} from '@angular/core';
import { NotificationContentService } from '../services/notification-content.service';
import { applyNotificationHighlight } from '../utils/notification-deep-link.util';

/**
 * Marks a DOM node as a notification deep-link target.
 * - When [lfHighlightId] matches [lfNotificationTarget], scrolls into view and highlights (once).
 * - When the element is on screen, marks matching unread notifications as read.
 */
@Directive({
  selector: '[lfNotificationTarget]',
  standalone: true,
  host: {
    '[class.lf-notification-highlight]': 'isHighlighted'
  }
})
export class NotificationTargetDirective implements AfterViewInit, OnChanges, OnDestroy {
  @Input({ required: true }) lfNotificationTarget!: number;
  /** ActionUrl path prefix used with relatedEntityId for mark-read (AND match on server). */
  @Input() lfNotificationPrefix?: string | null;
  /** One-shot highlight id captured from the notification navigation query. */
  @Input() lfHighlightId: number | null = null;

  isHighlighted = false;

  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly notificationContent = inject(NotificationContentService);
  private observer?: IntersectionObserver;
  private didScroll = false;
  private didMarkRead = false;

  ngAfterViewInit(): void {
    this.syncHighlight();
    this.observeVisibility();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['lfHighlightId'] || changes['lfNotificationTarget']) {
      this.syncHighlight();
    }
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }

  private syncHighlight(): void {
    this.isHighlighted =
      !!this.lfHighlightId &&
      !!this.lfNotificationTarget &&
      this.lfHighlightId === this.lfNotificationTarget;

    if (this.isHighlighted && !this.didScroll) {
      this.didScroll = true;
      // Defer so layout (lists after decrypt) can settle.
      requestAnimationFrame(() => applyNotificationHighlight(this.host.nativeElement));
    }
  }

  private observeVisibility(): void {
    if (typeof IntersectionObserver === 'undefined') {
      return;
    }

    this.observer = new IntersectionObserver(
      entries => {
        if (!entries.some(entry => entry.isIntersecting) || this.didMarkRead) {
          return;
        }
        this.didMarkRead = true;
        const prefix = this.lfNotificationPrefix?.trim();
        if (prefix) {
          this.notificationContent.markVisited(prefix, this.lfNotificationTarget);
        } else {
          this.notificationContent.markVisited('', this.lfNotificationTarget);
        }
        this.observer?.disconnect();
      },
      { threshold: 0.35 }
    );
    this.observer.observe(this.host.nativeElement);
  }
}
