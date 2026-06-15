import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { AuthService } from '../../services/auth.service';
import { User } from '../../models/user.model';

@Component({
  selector: 'app-sign-in-success',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './sign-in-success.component.html',
  styleUrl: './sign-in-success.component.css'
})
export class SignInSuccessComponent {
  currentUser: User | null = null;
  logoutButton: ActionBarButton;

  private router = inject(Router);
  private authService = inject(AuthService);

  constructor() {
    this.authService.currentUser$.subscribe(user => {
      this.currentUser = user;
    });

    this.logoutButton = {
      label: 'Logout',
      type: 'primary',
      onClick: () => this.logout()
    };
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/']);
  }
}
