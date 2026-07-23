import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { CrewmateService } from '../../../services/crewmate.service';
import { ToastService } from '../../../components/toast/toast.component';
import { NavigationService } from '../../../services/navigation.service';
import { CrewRoleDefinition } from '../../../models/crewmate.model';

@Component({
  selector: 'app-nominate-roles',
  standalone: true,
  imports: [CommonModule, FormsModule, PageLayoutComponent],
  templateUrl: './nominate-roles.component.html',
  styleUrl: './nominate-roles.component.css'
})
export class NominateRolesComponent implements OnInit {
  roles: CrewRoleDefinition[] = [];
  selectedRoles = new Set<string>();
  targetUsername = '';
  loading = true;
  submitting = false;
  errorMessage = '';
  termStartDate = '';
  termEndDate = '';
  backButton!: ActionBarButton;
  primaryButton: ActionBarButton | null = null;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private crewmateService = inject(CrewmateService);
  private toastService = inject(ToastService);
  private userId = 0;

  ngOnInit() {
    this.userId = Number(this.route.snapshot.paramMap.get('id'));
    if (!this.userId) {
      this.loading = false;
      this.errorMessage = 'Invalid crewmate.';
      return;
    }

    this.backButton = this.navigation.createBackButton(['/app/crew/crewmates', String(this.userId)]);

    this.loadData();
  }

  isSelected(role: string): boolean {
    return this.selectedRoles.has(role);
  }

  get requiresRepresentativeTerm(): boolean {
    return [...this.selectedRoles].some(role => {
      const definition = this.roles.find(r => r.role === role);
      return definition?.requiresTermDates === true;
    });
  }

  get minTermStartDate(): string {
    const tomorrow = new Date();
    tomorrow.setUTCDate(tomorrow.getUTCDate() + 1);
    return tomorrow.toISOString().slice(0, 10);
  }

  toggleRole(role: string) {
    if (this.selectedRoles.has(role)) {
      this.selectedRoles.delete(role);
    } else {
      this.selectedRoles.add(role);
    }

    if (!this.requiresRepresentativeTerm) {
      this.termStartDate = '';
      this.termEndDate = '';
    }

    this.updatePrimaryButton();
  }

  onTermDatesChanged() {
    this.updatePrimaryButton();
  }

  private loadData() {
    this.loading = true;
    this.errorMessage = '';

    this.crewmateService.getCrewmateProfile(this.userId).subscribe({
      next: profileResponse => {
        if (!profileResponse.success || !profileResponse.profile) {
          this.errorMessage = profileResponse.message || 'Failed to load crewmate.';
          this.loading = false;
          return;
        }

        this.targetUsername = profileResponse.profile.username;

        this.crewmateService.getRoleDefinitions().subscribe({
          next: rolesResponse => {
            if (!rolesResponse.success) {
              this.errorMessage = rolesResponse.message || 'Failed to load roles.';
              this.loading = false;
              return;
            }

            const held = new Set(profileResponse.profile!.electedRoles.map(r => r.role));
            this.roles = (rolesResponse.roles ?? []).filter(role => !held.has(role.role));
            this.loading = false;
            this.updatePrimaryButton();
          },
          error: () => {
            this.loading = false;
            this.errorMessage = 'Failed to load roles.';
            this.toastService.error(this.errorMessage);
          }
        });
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load crewmate.';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  private canSubmit(): boolean {
    if (this.submitting || this.selectedRoles.size === 0) {
      return false;
    }

    if (!this.requiresRepresentativeTerm) {
      return true;
    }

    if (!this.termStartDate || !this.termEndDate) {
      return false;
    }

    return this.termEndDate > this.termStartDate && this.termStartDate >= this.minTermStartDate;
  }

  private updatePrimaryButton() {
    this.primaryButton = {
      label: 'Nominate',
      type: 'primary',
      disabled: !this.canSubmit(),
      onClick: () => this.submitNomination()
    };
  }

  private toUtcIsoDate(dateOnly: string, endOfDay: boolean): string {
    return endOfDay
      ? `${dateOnly}T23:59:59.999Z`
      : `${dateOnly}T00:00:00.000Z`;
  }

  private submitNomination() {
    if (!this.canSubmit()) {
      return;
    }

    this.submitting = true;
    this.updatePrimaryButton();

    const termStart = this.requiresRepresentativeTerm
      ? this.toUtcIsoDate(this.termStartDate, false)
      : null;
    const termEnd = this.requiresRepresentativeTerm
      ? this.toUtcIsoDate(this.termEndDate, true)
      : null;

    this.crewmateService.nominateRoles(this.userId, [...this.selectedRoles], termStart, termEnd).subscribe({
      next: response => {
        this.submitting = false;
        this.updatePrimaryButton();

        if (!response.success) {
          this.toastService.error(response.message || 'Failed to submit nomination.');
          if (response.proposalId) {
            this.router.navigate(['/app/crew/proposals', response.proposalId]);
          }
          return;
        }

        this.toastService.success(response.message || 'Nomination proposal submitted.');
        if (response.proposalId) {
          this.router.navigate(['/app/crew/proposals', response.proposalId]);
        }
      },
      error: () => {
        this.submitting = false;
        this.updatePrimaryButton();
        this.toastService.error('Failed to submit nomination.');
      }
    });
  }
}
