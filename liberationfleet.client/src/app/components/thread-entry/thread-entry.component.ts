import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MentionTextComponent } from '../mention-text/mention-text.component';

@Component({
  selector: 'app-thread-entry',
  standalone: true,
  imports: [CommonModule, MentionTextComponent],
  templateUrl: './thread-entry.component.html',
  styleUrl: './thread-entry.component.css'
})
export class ThreadEntryComponent {
  @Input() authorName = '';
  @Input() createdAt: Date | string | null = null;
  @Input() body = '';
  @Input() compact = false;
}
