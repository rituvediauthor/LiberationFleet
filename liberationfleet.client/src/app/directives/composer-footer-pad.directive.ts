import { AfterViewInit, Directive, ElementRef, Input, OnDestroy } from '@angular/core';
import { readKeyboardInset } from '../utils/keyboard-inset.util';

@Directive({
  selector: '[appComposerFooterPad]',
  standalone: true
})
export class ComposerFooterPadDirective implements AfterViewInit, OnDestroy {
  @Input() appComposerFooterPad?: HTMLElement | ElementRef<HTMLElement> | null;

  private observer?: ResizeObserver;
  private footer?: HTMLElement;
  private scroll?: HTMLElement;
  private readonly onViewportChange = () => this.apply();

  constructor(private readonly host: ElementRef<HTMLElement>) {}

  ngAfterViewInit() {
    const footer = this.host.nativeElement;
    const scroll = this.resolveScroll();
    if (!scroll) {
      return;
    }

    this.footer = footer;
    this.scroll = scroll;
    if (typeof ResizeObserver !== 'undefined') {
      this.observer = new ResizeObserver(() => this.apply());
      this.observer.observe(footer);
    }
    window.visualViewport?.addEventListener('resize', this.onViewportChange);
    window.visualViewport?.addEventListener('scroll', this.onViewportChange);
    this.apply();
  }

  ngOnDestroy() {
    this.observer?.disconnect();
    window.visualViewport?.removeEventListener('resize', this.onViewportChange);
    window.visualViewport?.removeEventListener('scroll', this.onViewportChange);
  }

  private apply() {
    const footer = this.footer;
    const scroll = this.scroll;
    if (!footer || !scroll) {
      return;
    }
    const height = Math.ceil(footer.getBoundingClientRect().height);
    const keyboard = readKeyboardInset();
    scroll.style.paddingBottom = `${Math.max(height, 72) + keyboard}px`;
  }

  private resolveScroll(): HTMLElement | null {
    const target = this.appComposerFooterPad;
    if (!target) {
      return null;
    }
    return target instanceof HTMLElement ? target : target.nativeElement;
  }
}
