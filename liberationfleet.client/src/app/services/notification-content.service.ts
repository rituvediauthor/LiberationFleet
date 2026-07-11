import { Injectable, inject } from '@angular/core';
import { NotificationService } from './notification.service';

@Injectable({
  providedIn: 'root'
})
export class NotificationContentService {
  private notificationService = inject(NotificationService);

  markVisited(actionUrlPrefix: string, relatedEntityId?: number): void {
    this.notificationService.markReadForContent({
      actionUrlPrefix,
      relatedEntityId: relatedEntityId ?? null
    }).subscribe();
  }
}
