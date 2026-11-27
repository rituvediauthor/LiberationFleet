import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UserAvatarComponent } from '../user-avatar/user-avatar.component';

@Component({
  selector: 'app-author-identity',
  standalone: true,
  imports: [CommonModule, RouterLink, UserAvatarComponent],
  templateUrl: './author-identity.component.html',
  styleUrl: './author-identity.component.css'
})
export class AuthorIdentityComponent {
  /** Author user id used for crewmate navigation when content is not anonymous. */
  @Input() userId: number | null | undefined;
  @Input() username = '';
  /** Optional display override (e.g. "Alice > Bob" for nested replies). */
  @Input() displayName = '';
  @Input() avatarResourceId: string | null | undefined;
  @Input() crewId: number | null | undefined;
  @Input() fleetId: number | null | undefined;
  /** When true, never navigate (anonymous messages / proposals). */
  @Input() anonymous = false;
  @Input() size: 'xs' | 'sm' | 'md' | 'lg' = 'sm';
  /** Use <strong> for the name (comments) vs span.author (posts). */
  @Input() nameAsStrong = false;
  /**
   * Where to open the profile. Explicit crew/fleet always wins for routing.
   * `auto` prefers crew when crewId is set, otherwise fleet when fleetId is set.
   */
  @Input() linkScope: 'crew' | 'fleet' | 'auto' = 'auto';

  get label(): string {
    const override = this.displayName?.trim();
    if (override) {
      return override;
    }
    return this.username?.trim() || '';
  }

  get canNavigate(): boolean {
    return this.profileLink != null;
  }

  get resolvedScope(): 'crew' | 'fleet' | null {
    if (this.linkScope === 'crew' || this.linkScope === 'fleet') {
      return this.linkScope;
    }
    if (this.crewId != null && this.crewId > 0) {
      return 'crew';
    }
    if (this.fleetId != null && this.fleetId > 0) {
      return 'fleet';
    }
    return null;
  }

  /** Absolute router commands for the crewmate detail page, or null when not linkable. */
  get profileLink(): (string | number)[] | null {
    if (this.anonymous) {
      return null;
    }
    const id = Number(this.userId);
    if (!Number.isFinite(id) || id <= 0) {
      return null;
    }
    const scope = this.resolvedScope;
    if (!scope) {
      return null;
    }
    return scope === 'fleet'
      ? ['/app/fleet/crewmates', id]
      : ['/app/crew/crewmates', id];
  }

  /** Keep parent card/button handlers from also firing when opening a profile. */
  onLinkClick(event: Event): void {
    event.stopPropagation();
  }
}
