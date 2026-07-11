import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { CrewmateService } from '../../../services/crewmate.service';
import { CrewService } from '../../../services/crew.service';
import { ToastService } from '../../../components/toast/toast.component';
import {
  CrewmateListItem,
  formatLastActive,
  formatPlatformDisplay
} from '../../../models/crewmate.model';

@Component({
  selector: 'app-crewmate-list',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './crewmate-list.component.html',
  styleUrl: './crewmate-list.component.css'
})
export class CrewmateListComponent implements OnInit, OnDestroy {
  crewmates: CrewmateListItem[] = [];
  loading = true;
  errorMessage = '';
  canExportCrewData = false;
  backButton!: ActionBarButton;
  addButton!: ActionBarButton;

  private router = inject(Router);


  private navigation = inject(NavigationService);
  private crewmateService = inject(CrewmateService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);
  private activityIntervalId?: ReturnType<typeof setInterval>;
  activityTick = 0;

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/crew']);

    this.addButton = {
      label: 'Add crewmate',
      type: 'primary',
      onClick: () => this.toastService.error('Add crewmate is not available yet.')
    };

    this.activityIntervalId = setInterval(() => {
      this.activityTick++;
    }, 60000);

    this.crewService.getMembership().subscribe({
      next: membership => {
        this.canExportCrewData = !!membership.canExportCrewData;
      }
    });

    this.loadCrewmates();
  }

  ngOnDestroy() {
    if (this.activityIntervalId) {
      clearInterval(this.activityIntervalId);
    }
  }

  formatActivity(crewmate: CrewmateListItem): string {
    void this.activityTick;
    return formatLastActive(crewmate.lastLoginAt, crewmate.isSelf, crewmate.isPlaceholderMember);
  }

  formatPlatform(platform: CrewmateListItem['platformDisplay']): string {
    return formatPlatformDisplay(platform);
  }

  openCrewmate(crewmate: CrewmateListItem) {
    this.router.navigate(['/app/crew/crewmates', crewmate.userId]);
  }

  openKickedCrewmates() {
    this.router.navigate(['/app/crew/crewmates/kicked']);
  }

  exportCrewmateStates() {
    this.crewmateService.exportCrewmateStates().subscribe({
      next: blob => this.downloadBlob(blob, 'crewmate-states.json'),
      error: () => this.toastService.error('Failed to export crewmate states')
    });
  }

  private downloadBlob(blob: Blob, filename: string) {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(url);
    this.toastService.success('Export downloaded.');
  }

  private loadCrewmates() {
    this.loading = true;
    this.errorMessage = '';

    this.crewmateService.getCrewmates().subscribe({
      next: response => {
        if (!response.success) {
          this.errorMessage = response.message || 'Failed to load crewmates';
          this.crewmates = [];
        } else {
          this.crewmates = response.items ?? [];
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load crewmates';
        this.toastService.error(this.errorMessage);
      }
    });
  }
}
