import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MentionTextComponent } from '../mention-text/mention-text.component';
import { UserAvatarComponent } from '../user-avatar/user-avatar.component';

@Component({
  selector: 'app-thread-entry',
  standalone: true,
  imports: [CommonModule, MentionTextComponent, UserAvatarComponent],
  templateUrl: './thread-entry.component.html',
  styleUrl: './thread-entry.component.css'
})
export class ThreadEntryComponent {
  @Input() authorName = '';
  @Input() createdAt: Date | string | null = null;
  @Input() body = '';
  @Input() compact = false;
  @Input() anonymous = false;
  @Input() avatarResourceId: string | null | undefined;
  @Input() crewId: number | null | undefined;
  @Input() fleetId: number | null | undefined;
}
