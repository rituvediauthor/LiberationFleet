import { CommonModule } from '@angular/common';
import { Component, Input, inject } from '@angular/core';
import { Router } from '@angular/router';
import { UserAvatarComponent } from '../user-avatar/user-avatar.component';

@Component({
  selector: 'app-author-identity',
  standalone: true,
  imports: [CommonModule, UserAvatarComponent],
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
   * Where to open the profile. `auto` prefers crew when crewId is set,
   * otherwise fleet when fleetId is set.
   */
  @Input() linkScope: 'crew' | 'fleet' | 'auto' = 'auto';

  private readonly router = inject(Router);

  get label(): string {
    const override = this.displayName?.trim();
    if (override) {
      return override;
    }
    return this.username?.trim() || '';
  }

  get canNavigate(): boolean {
    if (this.anonymous) {
      return false;
    }
    const id = this.userId ?? 0;
    if (id <= 0) {
      return false;
    }
    return this.resolvedScope != null;
  }

  get resolvedScope(): 'crew' | 'fleet' | null {
    if (this.linkScope === 'crew') {
      return this.crewId != null && this.crewId > 0 ? 'crew' : null;
    }
    if (this.linkScope === 'fleet') {
      return this.fleetId != null && this.fleetId > 0 ? 'fleet' : null;
    }
    if (this.crewId != null && this.crewId > 0) {
      return 'crew';
    }
    if (this.fleetId != null && this.fleetId > 0) {
      return 'fleet';
    }
    return null;
  }

  openProfile(event: Event): void {
    if (!this.canNavigate) {
      return;
    }
    event.preventDefault();
    // Nested inside list/post open buttons — do not open the parent target.
    event.stopPropagation();

    const id = this.userId!;
    const commands =
      this.resolvedScope === 'fleet'
        ? ['/app/fleet/crewmates', id]
        : ['/app/crew/crewmates', id];
    void this.router.navigate(commands);
  }
}
