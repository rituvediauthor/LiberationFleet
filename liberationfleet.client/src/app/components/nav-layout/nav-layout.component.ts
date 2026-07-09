import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { FallibleFooterComponent } from '../fallible-footer/fallible-footer.component';
import { BrandLogoComponent } from '../brand-logo/brand-logo.component';

export type NavTab = 'crew' | 'friends' | 'notifications' | 'profile';

@Component({
  selector: 'app-nav-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, FallibleFooterComponent, BrandLogoComponent],
  templateUrl: './nav-layout.component.html',
  styleUrl: './nav-layout.component.css'
})
export class NavLayoutComponent {
  @Input() activeTab: NavTab = 'crew';
}
