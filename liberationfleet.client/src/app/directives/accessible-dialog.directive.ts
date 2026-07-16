import {
  Directive,
  ElementRef,
  HostListener,
  Input,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  inject
} from '@angular/core';
import { FocusTrap, FocusTrapFactory, InteractivityChecker } from '@angular/cdk/a11y';

/**
 * Trap focus inside a modal when active, restore focus on close, Escape to dismiss.
 * Put on the dialog panel (role=dialog / alertdialog), not the backdrop.
 */
@Directive({
  selector: '[appAccessibleDialog]',
  standalone: true
})
export class AccessibleDialogDirective implements OnChanges, OnDestroy {
  @Input('appAccessibleDialog') active = false;
  /** Optional callback when Escape is pressed while active. */
  @Input() appAccessibleDialogEscape?: () => void;

  private focusTrapFactory = inject(FocusTrapFactory);
  private elementRef = inject(ElementRef<HTMLElement>);
  private checker = inject(InteractivityChecker);
  private trap: FocusTrap | null = null;
  private previouslyFocused: HTMLElement | null = null;

  ngOnChanges(changes: SimpleChanges) {
    if (changes['active']) {
      if (this.active) {
        this.activate();
      } else {
        this.deactivate();
      }
    }
  }

  ngOnDestroy() {
    this.deactivate();
  }

  @HostListener('keydown', ['$event'])
  onKeydown(event: KeyboardEvent) {
    if (!this.active || event.key !== 'Escape') {
      return;
    }
    event.stopPropagation();
    event.preventDefault();
    this.appAccessibleDialogEscape?.();
  }

  private activate() {
    if (typeof document !== 'undefined') {
      this.previouslyFocused = document.activeElement as HTMLElement | null;
    }

    // Defer so *ngIf content is in the DOM / painted.
    queueMicrotask(() => {
      if (!this.active) {
        return;
      }
      this.trap?.destroy();
      this.trap = this.focusTrapFactory.create(this.elementRef.nativeElement);
      this.trap.focusInitialElementWhenReady().then(moved => {
        if (!moved) {
          const el = this.elementRef.nativeElement;
          if (!el.hasAttribute('tabindex')) {
            el.setAttribute('tabindex', '-1');
          }
          if (this.checker.isFocusable(el)) {
            el.focus();
          }
        }
      });
    });
  }

  private deactivate() {
    this.trap?.destroy();
    this.trap = null;
    if (this.previouslyFocused && typeof this.previouslyFocused.focus === 'function') {
      try {
        this.previouslyFocused.focus();
      } catch {
        /* element may be gone */
      }
    }
    this.previouslyFocused = null;
  }
}
