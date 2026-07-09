import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FallibleFooterComponent } from '../fallible-footer/fallible-footer.component';
import { BrandLogoComponent } from '../brand-logo/brand-logo.component';

export interface ActionBarButton {
  label: string;
  type: 'back' | 'primary' | 'secondary';
  disabled?: boolean;
  onClick?: () => void;
}

@Component({
  selector: 'app-page-layout',
  standalone: true,
  imports: [CommonModule, FallibleFooterComponent, BrandLogoComponent],
  templateUrl: './page-layout.component.html',
  styleUrl: './page-layout.component.css'
})
export class PageLayoutComponent {
  @Input() backButton: ActionBarButton | null = null;
  @Input() primaryButton: ActionBarButton | null = null;
  @Input() secondaryButton: ActionBarButton | null = null;
  @Input() fillHeight = false;
  @Input() brandNavButton = false;

  constructor(private router: Router) {}

  onBrandNavClick() {
    this.router.navigate(['/']);
  }
}
