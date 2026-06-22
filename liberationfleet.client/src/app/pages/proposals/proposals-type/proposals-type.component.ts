import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';

@Component({
  selector: 'app-proposals-type',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './proposals-type.component.html',
  styleUrl: './proposals-type.component.css'
})
export class ProposalsTypeComponent {
  backButton!: ActionBarButton;

  private router = inject(Router);

  constructor() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew'])
    };
  }

  openStatus(status: 'Approved' | 'Pending' | 'Rejected') {
    this.router.navigate(['/app/crew/proposals/list', status.toLowerCase()]);
  }
}
