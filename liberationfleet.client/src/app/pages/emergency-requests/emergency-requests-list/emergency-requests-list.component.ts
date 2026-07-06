import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { EmergencyRequestService } from '../../../services/emergency-request.service';
import { ToastService } from '../../../components/toast/toast.component';
import { EmergencyRequestListItem } from '../../../models/emergency-request.model';

@Component({
  selector: 'app-emergency-requests-list',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './emergency-requests-list.component.html',
  styleUrl: './emergency-requests-list.component.css'
})
export class EmergencyRequestsListComponent implements OnInit {
  items: EmergencyRequestListItem[] = [];
  loading = true;
  errorMessage = '';
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;

  private router = inject(Router);
  private emergencyRequestService = inject(EmergencyRequestService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew'])
    };

    this.createButton = {
      label: 'Create Emergency Request',
      type: 'primary',
      onClick: () => this.router.navigate(['/app/crew/emergency-requests/create'])
    };

    this.loadItems();
  }

  openRequest(item: EmergencyRequestListItem) {
    this.router.navigate(['/app/crew/emergency-requests', item.id]);
  }

  private loadItems() {
    this.loading = true;
    this.emergencyRequestService.getList().subscribe({
      next: response => {
        if (!response.success) {
          this.errorMessage = response.message || 'Failed to load emergency requests';
          this.items = [];
        } else {
          this.items = response.items ?? [];
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load emergency requests';
        this.toastService.error(this.errorMessage);
      }
    });
  }
}
