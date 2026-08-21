import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AccessibleDialogDirective } from '../../directives/accessible-dialog.directive';
import { UserAvatarComponent } from '../user-avatar/user-avatar.component';
import { ContentLiker } from '../../models/gift.model';

@Component({
  selector: 'app-content-likers-dialog',
  standalone: true,
  imports: [CommonModule, AccessibleDialogDirective, UserAvatarComponent],
  template: `
    <div
      class="likers-dialog-backdrop"
      *ngIf="open"
      (click)="onBackdropClick($event)">
      <div
        class="likers-dialog-card"
        role="dialog"
        aria-modal="true"
        [attr.aria-labelledby]="dialogTitleId"
        [appAccessibleDialog]="open"
        [appAccessibleDialogEscape]="onEscape">
        <h2 [id]="dialogTitleId">{{ title }}</h2>

        <p *ngIf="loading" class="state-text">Loading…</p>
        <p *ngIf="!loading && items.length === 0" class="state-text">No likes yet.</p>

        <ul class="likers-list" *ngIf="!loading && items.length > 0">
          <li *ngFor="let item of items">
            <app-user-avatar
              [resourceId]="item.avatarResourceId"
              [crewId]="crewId"
              [fleetId]="fleetId"
              [fallbackInitial]="item.username"
              size="sm"
              [alt]="item.username + ' avatar'">
            </app-user-avatar>
            <span class="liker-name">{{ item.username }}</span>
          </li>
        </ul>

        <div class="actions">
          <button type="button" class="btn secondary" (click)="closeDialog()">Close</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .likers-dialog-backdrop {
      position: fixed;
      inset: 0;
      z-index: 1200;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 20px;
      background: rgba(0, 0, 0, 0.45);
    }

    .likers-dialog-card {
      width: min(100%, 360px);
      max-height: min(70vh, 480px);
      overflow: auto;
      padding: 20px;
      border-radius: 12px;
      background: var(--lf-color-bg-surface);
      box-shadow: var(--lf-shadow-modal, 0 12px 40px rgba(0, 0, 0, 0.2));
    }

    h2 {
      margin: 0 0 12px;
      font-size: 18px;
      color: var(--lf-color-text-body);
    }

    .state-text {
      margin: 0 0 12px;
      color: var(--lf-color-text-secondary);
      font-size: 14px;
    }

    .likers-list {
      list-style: none;
      margin: 0 0 16px;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 10px;
    }

    .likers-list li {
      display: flex;
      align-items: center;
      gap: 10px;
    }

    .liker-name {
      font-size: 14px;
      font-weight: 600;
      color: var(--lf-color-text-body);
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
export class ContentLikersDialogComponent {
  @Input() open = false;
  @Input() items: ContentLiker[] = [];
  @Input() loading = false;
  @Input() title = 'Liked by';
  @Input() crewId: number | null = null;
  @Input() fleetId: number | null = null;

  @Output() close = new EventEmitter<void>();

  readonly dialogTitleId = 'content-likers-dialog-title';
  readonly onEscape = () => this.closeDialog();

  onBackdropClick(event: MouseEvent) {
    if ((event.target as HTMLElement).classList.contains('likers-dialog-backdrop')) {
      this.closeDialog();
    }
  }

  closeDialog() {
    this.close.emit();
  }
}
