import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { PaymentPlatformEditorComponent } from '../../../components/payment-platform-editor/payment-platform-editor.component';
import { CrewService } from '../../../services/crew.service';
import { CrewmateService } from '../../../services/crewmate.service';
import { ProfileService } from '../../../services/profile.service';
import { ToastService } from '../../../components/toast/toast.component';
import { CUSTOM_PLATFORM_OPTION_ID, PaymentPlatformAccount } from '../../../models/profile.model';
import { PaymentPlatformOption } from '../../../models/gift.model';

@Component({
  selector: 'app-add-non-crewmate',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent, PaymentPlatformEditorComponent],
  templateUrl: './add-non-crewmate.component.html',
  styleUrl: './add-non-crewmate.component.css'
})
export class AddNonCrewmateComponent implements OnInit {
  form!: FormGroup;
  paymentPlatforms: PaymentPlatformAccount[] = [];
  platformOptions: PaymentPlatformOption[] = [];
  saving = false;
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;

  private fb = inject(FormBuilder);
  private router = inject(Router);

  private navigation = inject(NavigationService);
  private crewService = inject(CrewService);
  private crewmateService = inject(CrewmateService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(256)]]
    });

    this.backButton = this.navigation.createBackButton(['/app/crew/gift-log/record']);

    this.updateSaveButton();

    this.crewService.getPaymentPlatforms().subscribe({
      next: platforms => {
        this.platformOptions = platforms;
        this.updateSaveButton();
      },
      error: () => this.toastService.error('Failed to load payment platforms')
    });

    this.form.valueChanges.subscribe(() => this.updateSaveButton());
  }

  addPaymentPlatform() {
    this.paymentPlatforms = [...this.paymentPlatforms, this.profileService.createPaymentPlatformAccount()];
    this.updateSaveButton();
  }

  removePaymentPlatform(accountId: number) {
    this.paymentPlatforms = this.paymentPlatforms.filter(account => account.id !== accountId);
    this.updateSaveButton();
  }

  setPreferredPlatform(accountId: number) {
    this.paymentPlatforms = this.paymentPlatforms.map(account => ({
      ...account,
      isPreferred: account.id === accountId
    }));
    this.updateSaveButton();
  }

  onPaymentPlatformChange() {
    this.updateSaveButton();
  }

  private updateSaveButton() {
    const disabled = this.saving || !this.canSave();
    this.saveButton = {
      label: 'Add non-member',
      type: 'primary',
      disabled,
      onClick: () => this.onSave()
    };
  }

  private canSave(): boolean {
    const name = String(this.form.get('name')?.value ?? '').trim();
    if (!name) {
      return false;
    }

    return this.paymentPlatforms.some(
      account =>
        account.handle.trim() &&
        (account.platformId > 0 || account.platformId === CUSTOM_PLATFORM_OPTION_ID && account.customPlatformName?.trim())
    );
  }

  private onSave() {
    if (!this.canSave() || this.saving) {
      return;
    }

    const name = String(this.form.get('name')?.value ?? '').trim();
    const platforms = this.paymentPlatforms
      .filter(
        account =>
          account.handle.trim() &&
          (account.platformId > 0 || account.platformId === CUSTOM_PLATFORM_OPTION_ID && account.customPlatformName?.trim())
      )
      .map(account => ({
        id: 0,
        platformId: account.platformId === CUSTOM_PLATFORM_OPTION_ID ? 0 : account.platformId,
        customPlatformName:
          account.platformId === CUSTOM_PLATFORM_OPTION_ID ? account.customPlatformName?.trim() : undefined,
        platform: account.platform,
        handle: account.handle.trim(),
        isPreferred: !!account.isPreferred
      }));

    if (!platforms.some(platform => platform.isPreferred)) {
      platforms[0].isPreferred = true;
    }

    this.saving = true;
    this.updateSaveButton();

    this.crewmateService.addPlaceholderCrewmate(name, platforms).subscribe({
      next: result => {
        if (result.success) {
          this.toastService.success(result.message || 'Non-member added');
          this.router.navigate(['/app/crew/gift-log/record']);
          return;
        }

        this.toastService.error(result.message || 'Failed to add non-member');
        this.saving = false;
        this.updateSaveButton();
      },
      error: error => {
        this.toastService.error(error?.error?.message || 'Failed to add non-member');
        this.saving = false;
        this.updateSaveButton();
      }
    });
  }
}
