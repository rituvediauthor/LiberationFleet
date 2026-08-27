import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-forum-engagement-bar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="engagement-bar" role="group" aria-label="Post engagement">
      <button
        type="button"
        class="engagement-btn"
        [class.liked]="liked"
        [disabled]="likeBusy"
        (click)="onLike($event)"
        [attr.aria-pressed]="liked"
        aria-label="Like post">
        <i
          class="fa-heart"
          [class.fa-solid]="liked"
          [class.fa-regular]="!liked"
          aria-hidden="true"></i>
        <span class="engagement-count">{{ likeCount }}</span>
      </button>
      <button
        *ngIf="likeCount > 0"
        type="button"
        class="view-likes-btn"
        (click)="onViewLikes($event)">
        View
      </button>
      <button
        type="button"
        class="engagement-btn"
        [class.static]="!commentClickable"
        [disabled]="!commentClickable"
        (click)="onComment($event)"
        aria-label="Comments">
        <i class="fa-solid fa-comment" aria-hidden="true"></i>
        <span class="engagement-count">{{ commentCount }}</span>
      </button>
    </div>
  `,
  styles: [`
    :host {
      display: block;
    }

    .engagement-bar {
      display: flex;
      align-items: center;
      gap: 16px;
      margin-top: 12px;
      padding-top: 12px;
      border-top: 1px solid var(--lf-color-border);
    }

    .engagement-btn {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      margin: 0;
      padding: 4px 2px;
      border: none;
      background: transparent;
      color: var(--lf-color-text-secondary);
      font-size: 14px;
      font-weight: 600;
      cursor: pointer;
    }

    .engagement-btn:hover:not(:disabled) {
      color: var(--lf-color-text-body);
    }

    .engagement-btn.liked {
      color: var(--lf-color-danger, #c0392b);
    }

    .engagement-btn.static,
    .engagement-btn:disabled.static {
      cursor: default;
      opacity: 1;
      color: var(--lf-color-text-secondary);
    }

    .engagement-btn:disabled:not(.static) {
      opacity: 0.6;
      cursor: wait;
    }

    .view-likes-btn {
      margin: 0;
      padding: 0;
      border: none;
      background: transparent;
      color: var(--lf-color-text-subtle);
      font-size: 12px;
      font-weight: 600;
      cursor: pointer;
    }

    .view-likes-btn:hover {
      color: var(--lf-color-text-body);
      text-decoration: underline;
    }

    .engagement-count {
      min-width: 1ch;
      font-variant-numeric: tabular-nums;
    }
  `]
})
export class ForumEngagementBarComponent {
  @Input() likeCount = 0;
  @Input() liked = false;
  @Input() commentCount = 0;
  @Input() commentClickable = true;
  @Input() likeBusy = false;

  @Output() likeClick = new EventEmitter<void>();
  @Output() commentClick = new EventEmitter<void>();
  @Output() viewLikesClick = new EventEmitter<void>();

  onLike(event: Event) {
    event.stopPropagation();
    event.preventDefault();
    if (this.likeBusy) {
      return;
    }
    this.likeClick.emit();
  }

  onViewLikes(event: Event) {
    event.stopPropagation();
    event.preventDefault();
    this.viewLikesClick.emit();
  }

  onComment(event: Event) {
    event.stopPropagation();
    event.preventDefault();
    if (!this.commentClickable) {
      return;
    }
    this.commentClick.emit();
  }
}
