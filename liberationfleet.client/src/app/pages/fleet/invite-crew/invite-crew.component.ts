import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { NavigationService } from '../../../services/navigation.service';
import { FleetService } from '../../../services/fleet.service';
import { ToastService } from '../../../components/toast/toast.component';
import { CrewLookupDto } from '../../../models/fleet.model';

const JOIN_CODE_LENGTH = 8;

@Component({
  selector: 'app-invite-crew',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './invite-crew.component.html',
  styleUrl: './invite-crew.component.css'
})
export class InviteCrewComponent implements OnInit {
  readonly joinCodeLength = JOIN_CODE_LENGTH;

  form = inject(FormBuilder).group({
    joinCode: ['']
  });

  foundCrew: CrewLookupDto | null = null;
  lookingUp = false;
  submitting = false;
  lookupMessage = '';

  backButton!: ActionBarButton;
  primaryButton!: ActionBarButton;

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/fleet/crews']);
    this.updatePrimaryButton();

    this.form.get('joinCode')?.valueChanges.subscribe(() => {
      this.foundCrew = null;
      this.lookupMessage = '';
      this.updatePrimaryButton();
    });
  }

  onJoinCodeInput(event: Event) {
    const input = event.target as HTMLInputElement;
    const normalized = input.value.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, JOIN_CODE_LENGTH);
    if (input.value !== normalized) {
      this.form.patchValue({ joinCode: normalized }, { emitEvent: true });
    }
  }

  get canLookup(): boolean {
    return this.normalizeJoinCode(this.form.get('joinCode')?.value).length === JOIN_CODE_LENGTH;
  }

  get canInvite(): boolean {
    return !!this.foundCrew
      && !this.foundCrew.alreadyInFleet
      && !this.foundCrew.isOwnCrew
      && !this.submitting;
  }

  lookupCrew() {
    if (!this.canLookup || this.lookingUp) {
      return;
    }

    this.lookingUp = true;
    this.foundCrew = null;
    this.updatePrimaryButton();

    this.fleetService.lookupCrewByJoinCode(this.normalizeJoinCode(this.form.get('joinCode')?.value)).subscribe({
      next: result => {
        this.lookingUp = false;
        if (!result.success || !result.crew) {
          this.lookupMessage = result.message || 'Crew not found.';
          this.updatePrimaryButton();
          return;
        }
        this.foundCrew = result.crew;
        this.lookupMessage = result.message;
        this.updatePrimaryButton();
      },
      error: error => {
        this.lookingUp = false;
        this.lookupMessage = error.error?.message || 'Lookup failed.';
        this.toastService.error(this.lookupMessage);
        this.updatePrimaryButton();
      }
    });
  }

  selectFoundCrew() {
    this.updatePrimaryButton();
  }

  private normalizeJoinCode(value: unknown): string {
    return String(value ?? '').toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, JOIN_CODE_LENGTH);
  }

  private updatePrimaryButton() {
    if (!this.foundCrew) {
      this.primaryButton = {
        label: 'Find Crew',
        type: 'primary',
        disabled: !this.canLookup || this.lookingUp,
        onClick: () => this.lookupCrew()
      };
      return;
    }

    this.primaryButton = {
      label: 'Invite Crew',
      type: 'primary',
      disabled: !this.canInvite,
      onClick: () => this.invite()
    };
  }

  private invite() {
    if (!this.canInvite) {
      return;
    }

    this.submitting = true;
    this.updatePrimaryButton();

    this.fleetService.inviteCrew(this.normalizeJoinCode(this.form.get('joinCode')?.value)).subscribe({
      next: result => {
        if (result.success) {
          this.toastService.success(result.message || 'Invitation sent');
          this.router.navigate(['/app/fleet/crews']);
          return;
        }
        this.toastService.error(result.message);
        this.submitting = false;
        this.updatePrimaryButton();
      },
      error: error => {
        this.toastService.error(error.error?.message || 'Failed to invite crew');
        this.submitting = false;
        this.updatePrimaryButton();
      }
    });
  }
}
