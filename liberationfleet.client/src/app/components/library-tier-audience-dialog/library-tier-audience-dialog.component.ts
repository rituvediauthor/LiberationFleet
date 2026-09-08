import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AccessibleDialogDirective } from '../../directives/accessible-dialog.directive';
import { UserAvatarComponent } from '../user-avatar/user-avatar.component';
import { LibraryPriorityTierAudienceMember } from '../../models/library.model';

@Component({
  selector: 'app-library-tier-audience-dialog',
  standalone: true,
  imports: [CommonModule, AccessibleDialogDirective, UserAvatarComponent],
  template: `
    <div
      class="audience-dialog-backdrop"
      *ngIf="open"
      (click)="onBackdropClick($event)">
      <div
        class="audience-dialog-card"
        role="dialog"
        aria-modal="true"
        [attr.aria-labelledby]="dialogTitleId"
        [appAccessibleDialog]="open"
        [appAccessibleDialogEscape]="onEscape">
        <h2 [id]="dialogTitleId">{{ title }}</h2>
        <p class="subtitle" *ngIf="subtitle">{{ subtitle }}</p>

        <p *ngIf="loading" class="state-text">Loading…</p>
        <p *ngIf="!loading && error" class="state-text error">{{ error }}</p>
        <p *ngIf="!loading && !error && items.length === 0" class="state-text">
          No eligible {{ audienceLabel }} in this selection.
        </p>

        <ul class="audience-list" *ngIf="!loading && !error && items.length > 0">
          <li *ngFor="let item of items">
            <app-user-avatar
              [resourceId]="item.avatarResourceId"
              [crewId]="crewId"
              [fleetId]="fleetId"
              [fallbackInitial]="item.username"
              size="sm"
              [alt]="item.username + ' avatar'">
            </app-user-avatar>
            <div class="audience-meta">
              <span class="audience-name">{{ item.username }}</span>
              <span class="audience-tier">Tier {{ item.tier }}</span>
            </div>
          </li>
        </ul>

        <div class="actions">
          <button type="button" class="btn secondary" (click)="closeDialog()">Close</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .audience-dialog-backdrop {
      position: fixed;
      inset: 0;
      z-index: 1200;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 20px;
      background: rgba(0, 0, 0, 0.45);
    }

    .audience-dialog-card {
      width: min(100%, 400px);
      max-height: min(70vh, 520px);
      overflow: auto;
      padding: 20px;
      border-radius: 12px;
      background: var(--lf-color-bg-surface);
      box-shadow: var(--lf-shadow-modal, 0 12px 40px rgba(0, 0, 0, 0.2));
    }

    h2 {
      margin: 0 0 6px;
      font-size: 18px;
      color: var(--lf-color-text-body);
    }

    .subtitle {
      margin: 0 0 12px;
      font-size: 13px;
      color: var(--lf-color-text-secondary);
      line-height: 1.4;
    }

    .state-text {
      margin: 0 0 12px;
      color: var(--lf-color-text-secondary);
      font-size: 14px;
    }

    .state-text.error {
      color: var(--lf-color-danger, #b42318);
    }

    .audience-list {
      list-style: none;
      margin: 0 0 16px;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 10px;
    }

    .audience-list li {
      display: flex;
      align-items: center;
      gap: 10px;
    }

    .audience-meta {
      display: flex;
      flex-direction: column;
      gap: 2px;
      min-width: 0;
    }

    .audience-name {
      font-size: 14px;
      font-weight: 600;
      color: var(--lf-color-text-body);
    }

    .audience-tier {
      font-size: 12px;
      color: var(--lf-color-text-secondary);
    }

    .actions {
      display: flex;
      justify-content: flex-end;
    }

    .btn {
      border: none;
      border-radius: 8px;
      padding: 8px 14px;
      font-size: 14px;
      font-weight: 600;
      cursor: pointer;
    }

    .btn.secondary {
      background: var(--lf-color-bg-muted);
      color: var(--lf-color-text-body);
    }
  `]
})
export class LibraryTierAudienceDialogComponent {
  @Input() open = false;
  @Input() items: LibraryPriorityTierAudienceMember[] = [];
  @Input() loading = false;
  @Input() error: string | null = null;
  @Input() title = 'Who can see this';
  @Input() subtitle: string | null = null;
  @Input() audienceLabel = 'crewmates';
  @Input() crewId: number | null = null;
  @Input() fleetId: number | null = null;

  @Output() close = new EventEmitter<void>();

  readonly dialogTitleId = 'library-tier-audience-dialog-title';
  readonly onEscape = () => this.closeDialog();

  onBackdropClick(event: MouseEvent) {
    if ((event.target as HTMLElement).classList.contains('audience-dialog-backdrop')) {
      this.closeDialog();
    }
  }

  closeDialog() {
    this.close.emit();
  }
}
