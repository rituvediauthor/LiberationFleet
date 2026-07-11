import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavigationService } from '../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { SecurityService } from '../../services/security.service';
import { ToastService } from '../../components/toast/toast.component';
import { SecurityAlertDto } from '../../models/security.model';

@Component({
  selector: 'app-security-alerts',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './security-alerts.component.html',
  styleUrl: './security-alerts.component.css'
})
export class SecurityAlertsComponent implements OnInit {
  alerts: SecurityAlertDto[] = [];
  loading = true;
  errorMessage = '';
  actionInProgress = false;
  backButton!: ActionBarButton;

  private router = inject(Router);


  private navigation = inject(NavigationService);
  private securityService = inject(SecurityService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/profile/preferences/security']);

    this.loadAlerts();
  }

  formatDate(value: string): string {
    return new Date(value).toLocaleString();
  }

  trustDevice(alert: SecurityAlertDto) {
    if (!alert.relatedDeviceId || this.actionInProgress) {
      return;
    }

    this.actionInProgress = true;
    this.securityService.trustDevice(alert.relatedDeviceId).subscribe({
      next: response => {
        this.actionInProgress = false;
        if (response.success) {
          this.toastService.success(response.message || 'Device registered');
          this.loadAlerts();
        } else {
          this.toastService.error(response.message || 'Failed to register device');
        }
      },
      error: () => {
        this.actionInProgress = false;
        this.toastService.error('Failed to register device');
      }
    });
  }

  blockDevice(alert: SecurityAlertDto) {
    if (!alert.relatedDeviceId || this.actionInProgress) {
      return;
    }

    this.actionInProgress = true;
    this.securityService.blockDevice(alert.relatedDeviceId).subscribe({
      next: response => {
        this.actionInProgress = false;
        if (response.success) {
          this.toastService.success(response.message || 'Device blocked');
          this.loadAlerts();
        } else {
          this.toastService.error(response.message || 'Failed to block device');
        }
      },
      error: () => {
        this.actionInProgress = false;
        this.toastService.error('Failed to block device');
      }
    });
  }

  private loadAlerts() {
    this.loading = true;
    this.securityService.getAlerts().subscribe({
      next: response => {
        this.alerts = response.success ? response.alerts : [];
        if (!response.success) {
          this.errorMessage = response.message || 'Failed to load security alerts';
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load security alerts';
      }
    });
  }
}
