import { AfterViewInit, Directive, ElementRef, Input, OnDestroy } from '@angular/core';
import {
  composerClearance,
  keyboardInsetChangeEventName,
  readAppliedKeyboardInset
} from '../utils/keyboard-inset.util';

@Directive({
  selector: '[appComposerFooterPad]',
  standalone: true
})
export class ComposerFooterPadDirective implements AfterViewInit, OnDestroy {
  @Input() appComposerFooterPad?: HTMLElement | ElementRef<HTMLElement> | null;

  private observer?: ResizeObserver;
  private footer?: HTMLElement;
  private scroll?: HTMLElement;
  private content?: HTMLElement;
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
    const content = scroll.firstElementChild;
    this.content = content instanceof HTMLElement ? content : undefined;
    if (typeof ResizeObserver !== 'undefined') {
      this.observer = new ResizeObserver(() => this.apply());
      this.observer.observe(footer);
    }
    window.visualViewport?.addEventListener('resize', this.onViewportChange);
    window.visualViewport?.addEventListener('scroll', this.onViewportChange);
    window.addEventListener(keyboardInsetChangeEventName, this.onViewportChange);
    this.apply();
  }

  ngOnDestroy() {
    this.observer?.disconnect();
    window.visualViewport?.removeEventListener('resize', this.onViewportChange);
    window.visualViewport?.removeEventListener('scroll', this.onViewportChange);
    window.removeEventListener(keyboardInsetChangeEventName, this.onViewportChange);
    this.scroll?.style.removeProperty('margin-bottom');
    this.scroll?.style.removeProperty('padding-bottom');
    this.content?.style.removeProperty('padding-bottom');
  }

  private apply() {
    const footer = this.footer;
    const scroll = this.scroll;
    if (!footer || !scroll) {
      return;
    }
    const distanceFromBottom = scroll.scrollHeight - scroll.scrollTop - scroll.clientHeight;
    const stickToBottom = distanceFromBottom < 80;
    const clearance = composerClearance(
      footer.getBoundingClientRect().height,
      readAppliedKeyboardInset()
    );
    // Shrink the scrollport to the visible band above the composer. Padding on
    // the scroll box itself is not part of the scroll range in a flex column.
    scroll.style.marginBottom = `${clearance}px`;
    scroll.style.paddingBottom = '0px';
    if (this.content) {
      this.content.style.paddingBottom = '16px';
    }
    if (stickToBottom) {
      scroll.scrollTop = scroll.scrollHeight;
    }
  }

  private resolveScroll(): HTMLElement | null {
    const target = this.appComposerFooterPad;
    if (!target) {
      return null;
    }
    return target instanceof HTMLElement ? target : target.nativeElement;
  }
}
