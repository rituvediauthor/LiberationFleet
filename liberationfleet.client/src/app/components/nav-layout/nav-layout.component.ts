import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';

export type NavTab = 'crew' | 'friends' | 'profile';

@Component({
  selector: 'app-nav-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  templateUrl: './nav-layout.component.html',
  styleUrl: './nav-layout.component.css'
})
export class NavLayoutComponent {
  @Input() activeTab: NavTab = 'crew';
}
