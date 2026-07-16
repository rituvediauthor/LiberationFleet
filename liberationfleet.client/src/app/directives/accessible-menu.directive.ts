import { Directive, EventEmitter, HostListener, Input, Output } from '@angular/core';

/**
 * Attach to an open menu panel. Escape closes; call sites still handle open/toggle.
 */
@Directive({
  selector: '[appAccessibleMenu]',
  standalone: true,
  host: {
    role: 'menu'
  }
})
export class AccessibleMenuDirective {
  @Input() appAccessibleMenu = true;
  @Output() menuEscape = new EventEmitter<void>();

  @HostListener('keydown.escape', ['$event'])
  onEscape(event: KeyboardEvent) {
    if (!this.appAccessibleMenu) {
      return;
    }
    event.preventDefault();
    event.stopPropagation();
    this.menuEscape.emit();
  }
}
