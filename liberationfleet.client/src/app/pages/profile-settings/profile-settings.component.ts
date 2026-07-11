import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavigationService } from '../../services/navigation.service';
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


  private navigation = inject(NavigationService);

  constructor() {
    this.backButton = this.navigation.createBackButton(['/app/profile']);
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

  goToTheme() {
    this.router.navigate(['/app/profile/preferences/theme']);
  }

  goToSecurity() {
    this.router.navigate(['/app/profile/preferences/security']);
  }

  goToPlaceholder(title: string) {
    this.router.navigate(['/app/profile/preferences/placeholder'], { queryParams: { title } });
  }
}
