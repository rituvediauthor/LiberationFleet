import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-char-counter',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span
      class="char-counter"
      [class.over-limit]="current > max"
      aria-live="polite">
      {{ current || 0 }} / {{ max }}
    </span>
  `,
  styles: [`
    .char-counter {
      display: block;
      margin-top: 0.25rem;
      font-size: 0.75rem;
      text-align: right;
      color: var(--text-muted, #6b7280);
    }
    .char-counter.over-limit {
      color: var(--danger, #dc2626);
      font-weight: 600;
    }
  `]
})
export class CharCounterComponent {
  @Input() current = 0;
  @Input() max = 0;
}
