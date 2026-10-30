import { Directive, EventEmitter, OnDestroy, Output } from '@angular/core';

/**
 * Closes an open kebab menu when the user clicks anywhere outside it.
 * Listens during capture so a click on a card that stops bubbling still dismisses the menu.
 * Clicks on the kebab button and inside the menu are left to the existing toggle and item handlers.
 */
@Directive({
  selector: '[appOutsideMenuClose]',
  standalone: true
})
export class OutsideMenuCloseDirective implements OnDestroy {
  @Output() appOutsideMenuClose = new EventEmitter<void>();

  private readonly onClick = (event: Event) => {
    if (clickStaysInsideKebab(event)) {
      return;
    }
    this.appOutsideMenuClose.emit();
  };

  constructor() {
    document.addEventListener('click', this.onClick, true);
  }

  ngOnDestroy() {
    document.removeEventListener('click', this.onClick, true);
  }
}

function clickStaysInsideKebab(event: Event): boolean {
  const target = event.target;
  const element = target instanceof Element
    ? target
    : target instanceof Node
      ? target.parentElement
      : null;
  return !!element?.closest('[aria-haspopup="menu"], [role="menu"]');
}
