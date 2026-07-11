import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges } from '@angular/core';
import { MentionTextMode, MentionTextSegment, parseMentionSegments } from '../../utils/mention.util';

@Component({
  selector: 'app-mention-text',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="mention-text">
      @for (segment of segments; track $index) {
        @if (segment.type === 'mention') {
          <strong class="mention-highlight">{{ segment.value }}</strong>
        } @else {
          {{ segment.value }}
        }
      }
    </span>
  `
})
export class MentionTextComponent implements OnChanges {
  @Input() text = '';
  @Input() mode: MentionTextMode = 'display';

  segments: MentionTextSegment[] = [];

  ngOnChanges(): void {
    this.segments = parseMentionSegments(this.text ?? '', this.mode);
  }
}
