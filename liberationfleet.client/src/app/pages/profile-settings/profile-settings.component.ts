import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';

@Component({
  selector: 'app-profile-settings',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './profile-settings.component.html',
  styleUrl: './profile-settings.component.css'
})
export class ProfileSettingsComponent {
  backButton: ActionBarButton;

  private router = inject(Router);

  constructor() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/profile'])
    };
  }

  goToNotifications() {
    this.router.navigate(['/app/profile/preferences/notifications']);
  }

  goToContent() {
    this.router.navigate(['/app/profile/preferences/content']);
  }

  goToVoice() {
    this.router.navigate(['/app/profile/preferences/voice']);
  }

  goToPlaceholder(title: string) {
    this.router.navigate(['/app/profile/preferences/placeholder'], { queryParams: { title } });
  }
}
