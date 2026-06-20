import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { switchMap } from 'rxjs';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { GiftService } from '../../services/gift.service';
import { ProfileService } from '../../services/profile.service';
import { ToastService } from '../../components/toast/toast.component';
import { CrewService } from '../../services/crew.service';
import { CUSTOM_PLATFORM_OPTION_ID, PaymentPlatformAccount, UserProfile } from '../../models/profile.model';
import { PaymentPlatformOption, SeasonReadyResult, SeasonSetupSaveResult } from '../../models/gift.model';

@Component({
  selector: 'app-season-setup',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './season-setup.component.html',
  styleUrl: './season-setup.component.css'
})
export class SeasonSetupComponent implements OnInit {
  form!: FormGroup;
  profile: UserProfile | null = null;
  platformOptions: PaymentPlatformOption[] = [];
  readyCount = 0;
  seasonReady = false;
  seasonStarted = false;
  isLoading = true;
  isSubmitting = false;
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private crewService = inject(CrewService);
  private giftService = inject(GiftService);
  readonly customPlatformOptionId = CUSTOM_PLATFORM_OPTION_ID;
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.form = this.fb.group({
      estimatedMonthlyContribution: ['', [Validators.required, Validators.min(0.01)]]
    });

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew'])
    };

    this.updateSaveButton();

    this.crewService.getPaymentPlatforms(true).subscribe({
      next: platforms => this.platformOptions = platforms
    });

    this.giftService.getSeasonStatus().subscribe({
      next: status => {
        this.readyCount = status.readyCount;
        this.seasonReady = status.userSeasonReady;
        this.seasonStarted = status.seasonStarted;
        if (status.estimatedMonthlyContribution) {
          this.form.patchValue({ estimatedMonthlyContribution: status.estimatedMonthlyContribution });
        }
        if (status.seasonStarted && status.userInSeason) {
          this.router.navigate(['/app/crew/gift-log']);
        } else if (status.seasonStarted && !status.userInSeason) {
          this.router.navigate(['/app/crew/join-season']);
        }
      }
    });

    this.profileService.getProfile().subscribe({
      next: profile => {
        this.profile = profile;
        this.isLoading = false;
        this.updateSaveButton();
      },
      error: () => {
        this.isLoading = false;
        this.toastService.error('Failed to load profile');
      }
    });

    this.form.statusChanges.subscribe(() => this.updateSaveButton());
    this.form.valueChanges.subscribe(() => this.updateSaveButton());
  }

  get paymentPlatforms(): PaymentPlatformAccount[] {
    return this.profile?.paymentPlatforms ?? [];
  }

  get hasValidPlatforms(): boolean {
    return this.paymentPlatforms.length > 0
      && this.paymentPlatforms.every(p => {
        const hasHandle = !!p.handle.trim();
        const hasPlatform = p.platformId > 0 || !!p.customPlatformName?.trim();
        return hasHandle && hasPlatform;
      });
  }

  isCustomPlatform(account: PaymentPlatformAccount): boolean {
    return this.profileService.isCustomPlatform(account);
  }

  addPaymentPlatform() {
    if (!this.profile) return;
    this.profileService.addPaymentPlatform(this.profile);
    this.updateSaveButton();
  }

  removePaymentPlatform(accountId: number) {
    if (!this.profile) return;
    this.profileService.removePaymentPlatform(this.profile, accountId);
    this.updateSaveButton();
  }

  onPaymentPlatformChange() {
    this.updateSaveButton();
  }

  onSeasonReadyChange() {
    this.updateSaveButton();
  }

  private updateSaveButton() {
    const readyRequiresPlatforms = this.seasonReady && !this.hasValidPlatforms;
    const disabled = this.isSubmitting || this.form.invalid || readyRequiresPlatforms;
    this.saveButton = {
      label: 'Save',
      type: 'primary',
      disabled,
      onClick: () => this.onSave()
    };
  }

  onSave() {
    if (!this.profile || this.form.invalid || this.isSubmitting) return;
    if (this.seasonReady && !this.hasValidPlatforms) {
      this.toastService.error('Register at least one payment platform before marking ready.');
      return;
    }

    this.isSubmitting = true;
    this.updateSaveButton();

    const estimate = Number(this.form.get('estimatedMonthlyContribution')?.value);
    const wantReady = this.seasonReady;

    this.profileService.saveProfile(this.profile).pipe(
      switchMap(saveResult => {
        if (!saveResult.success) {
          throw new Error(saveResult.message || 'Failed to save profile');
        }
        return this.giftService.saveSeasonSetup(estimate);
      }),
      switchMap(setupResult => {
        if (!setupResult.success) {
          throw new Error(setupResult.message || 'Failed to save season setup');
        }
        return wantReady
          ? this.giftService.markSeasonReady()
          : this.giftService.clearSeasonReady();
      })
    ).subscribe({
      next: readyResult => this.handleSaveComplete(readyResult, wantReady),
      error: err => {
        this.isSubmitting = false;
        this.toastService.error(err.message || 'Failed to save');
        this.updateSaveButton();
      }
    });
  }

  private handleSaveComplete(
    readyResult: SeasonReadyResult | SeasonSetupSaveResult,
    wantReady: boolean
  ) {
    this.isSubmitting = false;

    if (!readyResult.success) {
      this.toastService.error(readyResult.message);
      this.updateSaveButton();
      return;
    }

    this.toastService.success(wantReady ? readyResult.message : 'Season setup saved.');
    if (readyResult.status) {
      this.applyStatus(readyResult.status);
    }

    const seasonStarted = wantReady && 'seasonStarted' in readyResult && readyResult.seasonStarted;
    if (seasonStarted || readyResult.status?.userInSeason) {
      this.router.navigate(['/app/crew/gift-log']);
    }

    this.updateSaveButton();
  }

  private applyStatus(status: { readyCount: number; userSeasonReady: boolean; seasonStarted: boolean }) {
    this.readyCount = status.readyCount;
    this.seasonReady = status.userSeasonReady;
    this.seasonStarted = status.seasonStarted;
  }
}
