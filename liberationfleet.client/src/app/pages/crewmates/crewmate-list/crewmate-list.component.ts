import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { CrewmateService } from '../../../services/crewmate.service';
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
  backButton!: ActionBarButton;
  addButton!: ActionBarButton;

  private router = inject(Router);
  private crewmateService = inject(CrewmateService);
  private toastService = inject(ToastService);
  private activityIntervalId?: ReturnType<typeof setInterval>;
  activityTick = 0;

  ngOnInit() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew'])
    };

    this.addButton = {
      label: 'Add crewmate',
      type: 'primary',
      onClick: () => this.toastService.error('Add crewmate is not available yet.')
    };

    this.activityIntervalId = setInterval(() => {
      this.activityTick++;
    }, 60000);

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
