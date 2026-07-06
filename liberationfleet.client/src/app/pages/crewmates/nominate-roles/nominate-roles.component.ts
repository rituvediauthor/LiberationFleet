import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { CrewmateService } from '../../../services/crewmate.service';
import { ToastService } from '../../../components/toast/toast.component';
import { CrewRoleDefinition } from '../../../models/crewmate.model';

@Component({
  selector: 'app-nominate-roles',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
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
  backButton!: ActionBarButton;
  primaryButton: ActionBarButton | null = null;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
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

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew/crewmates', this.userId])
    };

    this.loadData();
  }

  isSelected(role: string): boolean {
    return this.selectedRoles.has(role);
  }

  toggleRole(role: string) {
    if (this.selectedRoles.has(role)) {
      this.selectedRoles.delete(role);
    } else {
      this.selectedRoles.add(role);
    }
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

  private updatePrimaryButton() {
    const count = this.selectedRoles.size;
    this.primaryButton = {
      label: 'Nominate',
      type: 'primary',
      disabled: this.submitting || count === 0,
      onClick: () => this.submitNomination()
    };
  }

  private submitNomination() {
    if (this.submitting || this.selectedRoles.size === 0) {
      return;
    }

    this.submitting = true;
    this.updatePrimaryButton();

    this.crewmateService.nominateRoles(this.userId, [...this.selectedRoles]).subscribe({
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
