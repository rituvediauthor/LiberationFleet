import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { UserAvatarComponent } from '../user-avatar/user-avatar.component';

@Component({
  selector: 'app-crewmate-id-card',
  standalone: true,
  imports: [CommonModule, UserAvatarComponent],
  templateUrl: './crewmate-id-card.component.html',
  styleUrl: './crewmate-id-card.component.css'
})
export class CrewmateIdCardComponent {
  @Input({ required: true }) username!: string;
  @Input() avatarResourceId: string | null | undefined;
  @Input() avatarPreviewUrl: string | null | undefined;
  @Input() crewId: number | null | undefined;
  @Input() roles: string[] = [];
  @Input() membershipStatus: boolean | null = null;
  @Input() priorityScore: number | null = null;
  @Input() libraryPriorityTier: number | null = null;
  @Input() inNeedOfAid: boolean | null = null;
  @Input() isSurvivalThresholdRecipient: boolean | null = null;
  @Input() crewName: string | null | undefined;
  @Input() fleetName: string | null | undefined;
  @Input() isPlaceholderMember = false;

  get roleDisplay(): string {
    if (this.isPlaceholderMember) {
      return this.roles.length ? `${this.roles.join(', ')} · Non-member` : 'Non-member';
    }
    return this.roles.length ? this.roles.join(', ') : 'None';
  }

  get priorityScoreDisplay(): string {
    if (this.priorityScore == null) {
      return '—';
    }
    return String(this.priorityScore);
  }

  get libraryPriorityTierDisplay(): string {
    if (this.libraryPriorityTier == null) {
      return '—';
    }
    return String(this.libraryPriorityTier);
  }

  yesNo(value: boolean | null | undefined): string {
    if (value == null) {
      return '—';
    }
    return value ? 'Yes' : 'No';
  }

  activeInactive(value: boolean | null | undefined): string {
    if (value == null) {
      return '—';
    }
    return value ? 'Active' : 'Inactive';
  }
}
