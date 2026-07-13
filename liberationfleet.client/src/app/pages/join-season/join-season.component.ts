import { Component, ElementRef, inject, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NavigationService } from '../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { PaymentPlatformEditorComponent } from '../../components/payment-platform-editor/payment-platform-editor.component';
import { GiftService } from '../../services/gift.service';
import { ProfileService } from '../../services/profile.service';
import { ToastService } from '../../components/toast/toast.component';
import { GiftLogEntry } from '../../models/gift.model';
import { CrewService } from '../../services/crew.service';
import { PaymentPlatformAccount, UserProfile } from '../../models/profile.model';
import { PaymentPlatformOption } from '../../models/gift.model';
import { mergePaymentPlatformOptions } from '../../utils/payment-platform-options.util';

@Component({
  selector: 'app-join-season',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, PageLayoutComponent, PaymentPlatformEditorComponent],
  templateUrl: './join-season.component.html',
  styleUrl: './join-season.component.css'
})
export class JoinSeasonComponent implements OnInit {
  @ViewChild('logContainer') logContainer?: ElementRef<HTMLDivElement>;

  form!: FormGroup;
  profile: UserProfile | null = null;
  platformOptions: PaymentPlatformOption[] = [];
  private basePlatformOptions: PaymentPlatformOption[] = [];
  entries: GiftLogEntry[] = [];
  loading = true;
  isSubmitting = false;
  backButton!: ActionBarButton;
  readyButton!: ActionBarButton;

  private fb = inject(FormBuilder);
  private router = inject(Router);

  private navigation = inject(NavigationService);
  private crewService = inject(CrewService);
  private giftService = inject(GiftService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.form = this.fb.group({
      estimatedMonthlyContribution: ['', [Validators.required, Validators.min(0.01)]]
    });

    this.backButton = this.navigation.createBackButton(['/app/crew']);

    this.updateReadyButton();

    this.giftService.getSeasonStatus().subscribe({
      next: status => {
        if (!status.seasonStarted) {
          if (this.isOnJoinSeasonRoute()) {
            void this.router.navigate(['/app/crew/season-setup'], { replaceUrl: true });
          }
        } else if (status.userInSeason) {
          if (this.isOnJoinSeasonRoute()) {
            void this.router.navigate(['/app/crew/gift-log'], { replaceUrl: true });
          }
        }
      }
    });

    this.crewService.getPaymentPlatforms(true).subscribe({
      next: platforms => {
        this.basePlatformOptions = platforms;
        this.syncPlatformOptions();
      }
    });

    this.giftService.getLogs().subscribe({
      next: page => {
        this.entries = page.items;
        setTimeout(() => this.scrollToBottom(), 0);
      }
    });

    this.profileService.getProfile().subscribe({
      next: profile => {
        this.profile = profile;
        this.syncPlatformOptions();
        this.loading = false;
        this.updateReadyButton();
      },
      error: () => {
        this.loading = false;
        this.toastService.error('Failed to load profile');
      }
    });

    this.form.statusChanges.subscribe(() => this.updateReadyButton());
    this.form.valueChanges.subscribe(() => this.updateReadyButton());
  }

  get paymentPlatforms(): PaymentPlatformAccount[] {
    return this.profile?.paymentPlatforms ?? [];
  }

  addPaymentPlatform() {
    if (!this.profile) return;
    this.profileService.addPaymentPlatform(this.profile);
    this.updateReadyButton();
  }

  removePaymentPlatform(accountId: number) {
    if (!this.profile) return;
    this.profileService.removePaymentPlatform(this.profile, accountId);
    this.updateReadyButton();
  }

  onPaymentPlatformChange() {
    this.updateReadyButton();
  }

  formatTimestamp(date: Date): string {
    return new Date(date).toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit'
    });
  }

  private scrollToBottom() {
    const el = this.logContainer?.nativeElement;
    if (el) {
      el.scrollTop = el.scrollHeight;
    }
  }

  private updateReadyButton() {
    const hasPlatforms = this.paymentPlatforms.length > 0
      && this.paymentPlatforms.every(p => {
        const hasHandle = !!p.handle.trim();
        const hasPlatform = p.platformId > 0 || !!p.customPlatformName?.trim();
        return hasHandle && hasPlatform;
      });
    const disabled = this.isSubmitting || this.form.invalid || !hasPlatforms;
    this.readyButton = {
      label: 'Ready',
      type: 'primary',
      disabled,
      onClick: () => this.onReady()
    };
  }

  onReady() {
    if (!this.profile || this.form.invalid || this.isSubmitting) return;

    this.isSubmitting = true;
    this.updateReadyButton();
    const estimate = Number(this.form.get('estimatedMonthlyContribution')?.value);

    this.profileService.saveProfile(this.profile).subscribe({
      next: saveResult => {
        if (!saveResult.success) {
          this.toastService.error(saveResult.message || 'Failed to save profile');
          this.isSubmitting = false;
          this.updateReadyButton();
          return;
        }

        if (saveResult.profile) {
          this.profile = saveResult.profile;
          this.syncPlatformOptions();
        }

        this.giftService.saveSeasonSetup(estimate).subscribe({
          next: setupResult => {
            if (!setupResult.success) {
              this.toastService.error(setupResult.message);
              this.isSubmitting = false;
              this.updateReadyButton();
              return;
            }

            this.giftService.markSeasonReady().subscribe({
              next: result => {
                this.isSubmitting = false;
                if (!result.success) {
                  this.toastService.error(result.message);
                  this.updateReadyButton();
                  return;
                }
                this.toastService.success(result.message);
                if (this.isOnJoinSeasonRoute()) {
                  void this.router.navigate(['/app/crew/gift-log'], { replaceUrl: true });
                }
              },
              error: () => {
                this.isSubmitting = false;
                this.toastService.error('Failed to join season');
                this.updateReadyButton();
              }
            });
          },
          error: () => {
            this.isSubmitting = false;
            this.toastService.error('Failed to save season setup');
            this.updateReadyButton();
          }
        });
      },
      error: () => {
        this.isSubmitting = false;
        this.toastService.error('Failed to save profile');
        this.updateReadyButton();
      }
    });
  }

  private isOnJoinSeasonRoute(): boolean {
    return this.router.url.split('?')[0] === '/app/crew/join-season';
  }

  private syncPlatformOptions() {
    this.platformOptions = mergePaymentPlatformOptions(
      this.basePlatformOptions,
      this.profile?.paymentPlatforms ?? []
    );
  }
}
