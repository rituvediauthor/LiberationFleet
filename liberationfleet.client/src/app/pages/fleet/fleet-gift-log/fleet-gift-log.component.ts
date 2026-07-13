import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { NavigationService } from '../../../services/navigation.service';
import { FleetService } from '../../../services/fleet.service';
import { ToastService } from '../../../components/toast/toast.component';
import { GiftLogEntry } from '../../../models/gift.model';
import { NotificationContentService } from '../../../services/notification-content.service';

@Component({
  selector: 'app-fleet-gift-log',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './fleet-gift-log.component.html',
  styleUrl: './fleet-gift-log.component.css'
})
export class FleetGiftLogComponent implements OnInit {
  entries: GiftLogEntry[] = [];
  loading = true;
  errorMessage = '';
  backButton!: ActionBarButton;
  recordButton: ActionBarButton | null = null;

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);
  private notificationContent = inject(NotificationContentService);

  ngOnInit() {
    this.notificationContent.markVisited('/app/fleet/gift-log');
    this.backButton = this.navigation.createBackButton(['/app/fleet']);
    this.loadStatus();
    this.loadGiftLog();
  }

  formatTimestamp(value: Date | string): string {
    return new Date(value).toLocaleString();
  }

  private loadStatus() {
    this.fleetService.getStatus().subscribe({
      next: status => {
        if (status.hasFleet && status.allowCrossCrewGiving) {
          this.recordButton = {
            label: 'Record gift',
            type: 'primary',
            onClick: () => this.router.navigate(['/app/fleet/gift-log/record'])
          };
        } else {
          this.recordButton = null;
        }
      },
      error: () => {
        this.recordButton = null;
      }
    });
  }

  private loadGiftLog() {
    this.loading = true;
    this.errorMessage = '';
    this.fleetService.getGiftLog({ limit: 50 }).subscribe({
      next: result => {
        this.loading = false;
        if (!result.success) {
          this.errorMessage = result.message || 'Failed to load gift log';
          this.entries = [];
          return;
        }
        this.entries = result.items ?? [];
      },
      error: error => {
        this.loading = false;
        this.errorMessage = error.error?.message || 'Failed to load gift log';
        this.toastService.error(this.errorMessage);
      }
    });
  }
}
