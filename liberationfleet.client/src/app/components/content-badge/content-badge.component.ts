import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { formatBadgeCount } from '../../utils/notification-area.util';

@Component({
  selector: 'app-content-badge',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span *ngIf="label" class="content-badge" [attr.aria-label]="ariaLabel">{{ label }}</span>
  `,
  styles: [`
    .content-badge {
      position: absolute;
      top: -7px;
      right: -9px;
      min-width: 18px;
      height: 18px;
      padding: 0 5px;
      border-radius: 999px;
      background: var(--lf-color-danger);
      color: var(--lf-color-text-inverse);
      font-size: 11px;
      font-weight: 700;
      line-height: 18px;
      text-align: center;
      pointer-events: none;
    }
  `]
})
export class ContentBadgeComponent {
  @Input() count = 0;
  @Input() showPlusAtNine = false;
  @Input() ariaLabelPrefix = 'Unread notifications';

  get label(): string {
    return formatBadgeCount(this.count, this.showPlusAtNine);
  }

  get ariaLabel(): string {
    if (!this.label) {
      return '';
    }
    return `${this.ariaLabelPrefix}: ${this.count}`;
  }
}
