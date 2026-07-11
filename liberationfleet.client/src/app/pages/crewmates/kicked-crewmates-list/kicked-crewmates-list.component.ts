import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { CrewmateService } from '../../../services/crewmate.service';
import { ToastService } from '../../../components/toast/toast.component';
import { KickedCrewmateListItem } from '../../../models/crewmate.model';

@Component({
  selector: 'app-kicked-crewmates-list',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './kicked-crewmates-list.component.html',
  styleUrl: './kicked-crewmates-list.component.css'
})
export class KickedCrewmatesListComponent implements OnInit {
  kickedCrewmates: KickedCrewmateListItem[] = [];
  loading = true;
  errorMessage = '';
  actionUserId: number | null = null;
  backButton!: ActionBarButton;

  private router = inject(Router);


  private navigation = inject(NavigationService);
  private crewmateService = inject(CrewmateService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/crew/crewmates']);

    this.loadKickedCrewmates();
  }

  allowRejoin(crewmate: KickedCrewmateListItem, event: Event) {
    event.stopPropagation();
    if (this.actionUserId !== null) {
      return;
    }

    this.actionUserId = crewmate.userId;
    this.crewmateService.allowRejoin(crewmate.userId).subscribe({
      next: response => {
        this.actionUserId = null;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to submit rejoin proposal');
          if (response.proposalId) {
            this.router.navigate(['/app/crew/proposals', response.proposalId]);
          }
          return;
        }

        this.toastService.success(response.message || 'Rejoin proposal submitted');
        if (response.proposalId) {
          this.router.navigate(['/app/crew/proposals', response.proposalId]);
        }
      },
      error: () => {
        this.actionUserId = null;
        this.toastService.error('Failed to submit rejoin proposal');
      }
    });
  }

  private loadKickedCrewmates() {
    this.loading = true;
    this.errorMessage = '';

    this.crewmateService.getKickedCrewmates().subscribe({
      next: response => {
        if (!response.success) {
          this.errorMessage = response.message || 'Failed to load kicked crewmates';
          this.kickedCrewmates = [];
        } else {
          this.kickedCrewmates = response.items ?? [];
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load kicked crewmates';
        this.toastService.error(this.errorMessage);
      }
    });
  }
}
