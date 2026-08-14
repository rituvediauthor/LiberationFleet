import { AfterViewInit, Directive, ElementRef, Input, OnDestroy } from '@angular/core';

@Directive({
  selector: '[appComposerFooterPad]',
  standalone: true
})
export class ComposerFooterPadDirective implements AfterViewInit, OnDestroy {
  @Input() appComposerFooterPad?: HTMLElement | ElementRef<HTMLElement> | null;

  private observer?: ResizeObserver;

  constructor(private readonly host: ElementRef<HTMLElement>) {}

  ngAfterViewInit() {
    const footer = this.host.nativeElement;
    const scroll = this.resolveScroll();
    if (!scroll || typeof ResizeObserver === 'undefined') {
      return;
    }

    this.observer = new ResizeObserver(() => this.apply(footer, scroll));
    this.observer.observe(footer);
    this.apply(footer, scroll);
  }

  ngOnDestroy() {
    this.observer?.disconnect();
  }

  private apply(footer: HTMLElement, scroll: HTMLElement) {
    const height = Math.ceil(footer.getBoundingClientRect().height);
    scroll.style.paddingBottom = `${Math.max(height, 72)}px`;
  }

  private resolveScroll(): HTMLElement | null {
    const target = this.appComposerFooterPad;
    if (!target) {
      return null;
    }
    return target instanceof HTMLElement ? target : target.nativeElement;
  }
}
