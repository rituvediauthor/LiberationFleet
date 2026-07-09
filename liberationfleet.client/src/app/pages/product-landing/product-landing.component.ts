import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { BrandLogoComponent } from '../../components/brand-logo/brand-logo.component';

@Component({
  selector: 'app-product-landing',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, BrandLogoComponent],
  templateUrl: './product-landing.component.html',
  styleUrl: './product-landing.component.css'
})
export class ProductLandingComponent {
  signInButton: ActionBarButton;

  constructor(private router: Router) {
    this.signInButton = {
      label: 'Sign In',
      type: 'primary',
      onClick: () => this.navigateToSignIn()
    };
  }

  private navigateToSignIn() {
    this.router.navigate(['/sign-in']);
  }
}
