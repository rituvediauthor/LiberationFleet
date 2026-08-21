import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-forum-comment-like',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="comment-like-wrap">
      <button
        type="button"
        class="comment-like-btn"
        [class.liked]="liked"
        [disabled]="busy"
        (click)="onToggle($event)"
        [attr.aria-pressed]="liked"
        aria-label="Like comment">
        <i class="fa-solid fa-heart" aria-hidden="true"></i>
        <span class="like-count">{{ likeCount }}</span>
      </button>
      <button
        *ngIf="likeCount > 0"
        type="button"
        class="view-likes-link"
        (click)="onViewLikes($event)">
        View
      </button>
    </div>
  `,
  styles: [`
    :host {
      display: inline-flex;
      flex-shrink: 0;
    }

    .comment-like-wrap {
      display: inline-flex;
      align-items: center;
      gap: 6px;
    }

    .comment-like-btn {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      margin: 0;
      padding: 4px 6px;
      border: none;
      border-radius: 6px;
      background: transparent;
      color: var(--lf-color-text-subtle);
      font-size: 12px;
      font-weight: 600;
      cursor: pointer;
    }

    .comment-like-btn:hover:not(:disabled) {
      background: var(--lf-color-bg-hover);
      color: var(--lf-color-text-body);
    }

    .comment-like-btn.liked {
      color: var(--lf-color-danger, #c0392b);
    }

    .comment-like-btn:disabled {
      opacity: 0.6;
      cursor: wait;
    }

    .like-count {
      font-variant-numeric: tabular-nums;
    }

    .view-likes-link {
      margin: 0;
      padding: 0;
      border: none;
      background: transparent;
      color: var(--lf-color-text-subtle);
      font-size: 11px;
      font-weight: 600;
      cursor: pointer;
    }

    .view-likes-link:hover {
      color: var(--lf-color-text-body);
      text-decoration: underline;
    }
  `]
})
export class ForumCommentLikeComponent {
  @Input() likeCount = 0;
  @Input() liked = false;
  @Input() busy = false;
  @Output() likeClick = new EventEmitter<void>();
  @Output() viewLikesClick = new EventEmitter<void>();

  onToggle(event: Event) {
    event.stopPropagation();
    event.preventDefault();
    if (this.busy) {
      return;
    }
    this.likeClick.emit();
  }

  onViewLikes(event: Event) {
    event.stopPropagation();
    event.preventDefault();
    this.viewLikesClick.emit();
  }
}
