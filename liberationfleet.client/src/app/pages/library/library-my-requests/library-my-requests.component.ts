import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { NotificationContentService } from '../../../services/notification-content.service';

@Component({
  selector: 'app-library-my-requests',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './library-my-requests.component.html',
  styleUrl: './library-my-requests.component.css'
})
export class LibraryMyRequestsComponent {
  backButton!: ActionBarButton;

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private notificationContent = inject(NotificationContentService);

  constructor() {
    this.backButton = this.navigation.createBackButton(['/app/crew/library-of-things']);
    this.notificationContent.markVisited('/app/crew/library-of-things/requests/mine');
  }

  openPending() {
    void this.router.navigate(['/app/crew/library-of-things/requests/mine/pending']);
  }

  openFulfilled() {
    void this.router.navigate(['/app/crew/library-of-things/requests/mine/fulfilled']);
  }

  openDenied() {
    void this.router.navigate(['/app/crew/library-of-things/requests/denied']);
  }
}
