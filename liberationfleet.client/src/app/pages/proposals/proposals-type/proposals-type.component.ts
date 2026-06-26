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
  createButton!: ActionBarButton;

  private router = inject(Router);

  constructor() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew'])
    };

    this.createButton = {
      label: 'Create Proposal',
      type: 'primary',
      onClick: () => this.router.navigate(['/app/crew/proposals/create'])
    };
  }

  openStatus(status: 'Approved' | 'Pending' | 'Rejected') {
    this.router.navigate(['/app/crew/proposals/list', status.toLowerCase()]);
  }
}
