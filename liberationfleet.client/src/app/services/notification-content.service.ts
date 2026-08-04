import { Injectable, inject } from '@angular/core';
import { NotificationService } from './notification.service';

@Injectable({
  providedIn: 'root'
})
export class NotificationContentService {
  private notificationService = inject(NotificationService);

  markVisited(actionUrlPrefix: string, relatedEntityId?: number): void {
    const prefix = actionUrlPrefix?.trim() || null;
    if (!prefix && relatedEntityId == null) {
      return;
    }

    this.notificationService.markReadForContent({
      actionUrlPrefix: prefix,
      relatedEntityId: relatedEntityId ?? null
    }).subscribe({
      next: () => this.notificationService.refreshBadges(true),
      error: () => undefined
    });
  }
}
