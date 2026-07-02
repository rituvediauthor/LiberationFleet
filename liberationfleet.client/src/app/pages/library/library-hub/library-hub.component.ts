import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';

@Component({
  selector: 'app-library-hub',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './library-hub.component.html',
  styleUrl: './library-hub.component.css'
})
export class LibraryHubComponent {
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;

  private router = inject(Router);

  constructor() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew'])
    };

    this.createButton = {
      label: 'Create Offering',
      type: 'primary',
      onClick: () => this.router.navigate(['/app/crew/library-of-things/offerings/create'])
    };
  }

  openSection(section: 'requests' | 'durable' | 'consumable' | 'services' | 'mine') {
    this.router.navigate(['/app/crew/library-of-things', section]);
  }

  openMyRequests() {
    this.router.navigate(['/app/crew/library-of-things/requests/mine']);
  }
}
