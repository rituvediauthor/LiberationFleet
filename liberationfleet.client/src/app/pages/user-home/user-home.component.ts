import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { NavLayoutComponent } from '../../components/nav-layout/nav-layout.component';

@Component({
  selector: 'app-user-home',
  standalone: true,
  imports: [NavLayoutComponent],
  templateUrl: './user-home.component.html',
  styleUrl: './user-home.component.css'
})
export class UserHomeComponent {
  private router = inject(Router);

  goToUserProfile() {
    this.router.navigate(['/app/profile/user']);
  }

  goToGiftHistory() {
    this.router.navigate(['/app/profile/gift-history']);
  }

  goToActivityCenter() {
    this.router.navigate(['/app/profile/activity']);
  }

  goToPreferences() {
    this.router.navigate(['/app/profile/preferences']);
  }

  goToDonate() {
    this.router.navigate(['/app/donate']);
  }
}
