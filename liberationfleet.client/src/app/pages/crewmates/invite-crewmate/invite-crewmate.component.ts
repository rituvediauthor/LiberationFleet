import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { NavigationService } from '../../../services/navigation.service';
import { CrewService } from '../../../services/crew.service';
import { CryptoSessionService } from '../../../services/crypto/crypto-session.service';
import { ToastService } from '../../../components/toast/toast.component';
import { InviteCandidate } from '../../../models/crew.model';

@Component({
  selector: 'app-invite-crewmate',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './invite-crewmate.component.html',
  styleUrl: './invite-crewmate.component.css'
})
export class InviteCrewmateComponent implements OnInit {
  form = inject(FormBuilder).group({
    username: [''],
    friendsOnly: [false]
  });

  candidates: InviteCandidate[] = [];
  selected: InviteCandidate | null = null;
  loading = false;
  submitting = false;
  hasLoaded = false;
  message = '';

  backButton!: ActionBarButton;
  primaryButton!: ActionBarButton;

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private crewService = inject(CrewService);
  private cryptoSession = inject(CryptoSessionService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/crew/crewmates']);
    this.updatePrimaryButton();

    this.form.valueChanges.pipe(debounceTime(350), distinctUntilChanged()).subscribe(() => {
      this.loadCandidates();
    });

    this.loadCandidates();
  }

  selectCandidate(candidate: InviteCandidate) {
    this.selected = candidate;
    this.updatePrimaryButton();
  }

  private loadCandidates() {
    this.loading = true;
    const username = String(this.form.get('username')?.value ?? '').trim();
    const friendsOnly = !!this.form.get('friendsOnly')?.value;

    this.crewService.getInviteCandidates(username || undefined, friendsOnly).subscribe({
      next: result => {
        this.loading = false;
        this.hasLoaded = true;
        if (!result.success) {
          this.message = result.message;
          this.candidates = [];
          this.selected = null;
          this.updatePrimaryButton();
          return;
        }

        this.message = result.message;
        this.candidates = result.items ?? [];
        if (this.selected && !this.candidates.some(c => c.userId === this.selected!.userId)) {
          this.selected = null;
        }
        this.updatePrimaryButton();
      },
      error: error => {
        this.loading = false;
        this.hasLoaded = true;
        this.toastService.error(error.error?.message || 'Failed to load candidates');
        this.updatePrimaryButton();
      }
    });
  }

  private updatePrimaryButton() {
    this.primaryButton = {
      label: 'Invite to Crew',
      type: 'primary',
      disabled: !this.selected || this.submitting || this.loading,
      onClick: () => this.onInvite()
    };
  }

  private onInvite() {
    if (!this.selected || this.submitting) {
      return;
    }

    this.submitting = true;
    this.updatePrimaryButton();
    const inviteeUserId = this.selected.userId;

    this.crewService.inviteCrewmate(inviteeUserId).subscribe({
      next: async result => {
        if (result.success) {
          await this.tryDistributeCrewKey(inviteeUserId);
          this.toastService.success(result.message || 'Invitation sent');
          this.router.navigate(['/app/crew/crewmates']);
          return;
        }
        this.toastService.error(result.message);
        this.submitting = false;
        this.updatePrimaryButton();
      },
      error: error => {
        this.toastService.error(error.error?.message || 'Failed to send invitation');
        this.submitting = false;
        this.updatePrimaryButton();
      }
    });
  }

  private async tryDistributeCrewKey(inviteeUserId: number): Promise<void> {
    if (!this.cryptoSession.isUnlocked()) {
      return;
    }

    try {
      const membership = await firstValueFrom(this.crewService.getMembership());
      if (!membership.hasCrew || !membership.crewId) {
        return;
      }
      await this.cryptoSession.distributeCrewKeyToUser(membership.crewId, inviteeUserId);
    } catch {
      // Invitation already succeeded; key sync can retry when a crewmate opens the app.
    }
  }
}
