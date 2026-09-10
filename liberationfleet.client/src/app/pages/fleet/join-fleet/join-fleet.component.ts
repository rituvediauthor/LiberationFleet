import { Component, inject, OnInit } from '@angular/core';
import { debounceTime } from 'rxjs/operators';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { HubLoadingComponent } from '../../../components/hub-loading/hub-loading.component';
import { CountrySelectComponent } from '../../../components/country-select/country-select.component';
import { CryptoUnlockDialogComponent } from '../../../components/crypto-unlock-dialog/crypto-unlock-dialog.component';
import { FleetService } from '../../../services/fleet.service';
import { NavigationService } from '../../../services/navigation.service';
import { ToastService } from '../../../components/toast/toast.component';
import { ProfileService } from '../../../services/profile.service';
import { ProfileLocationService } from '../../../services/profile-location.service';
import { CryptoSessionService } from '../../../services/crypto/crypto-session.service';
import { Fleet, FleetScope, PublicFleetRule } from '../../../models/fleet.model';
import { isControlInvalidForA11y } from '../../../utils/a11y-form.util';
import { isValidPostalCode, normalizePostalCode } from '../../../constants/countries';

type JoinMode = 'find' | 'code';
type JoinStep = 'select' | 'rules';
const JOIN_CODE_LENGTH = 8;

@Component({
  selector: 'app-join-fleet',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageLayoutComponent,
    RouterLink,
    HubLoadingComponent,
    CountrySelectComponent,
    CryptoUnlockDialogComponent
  ],
  templateUrl: './join-fleet.component.html',
  styleUrl: './join-fleet.component.css'
})
export class JoinFleetComponent implements OnInit {
  readonly joinCodeLength = JOIN_CODE_LENGTH;

  form: FormGroup;
  backButton: ActionBarButton;
  primaryButton!: ActionBarButton;

  joinStep: JoinStep = 'select';
  searchResults: Fleet[] = [];
  selectedFleet: Fleet | null = null;
  currentPage = 1;
  totalPages = 0;
  totalCount = 0;
  searchMessage = '';
  isSearching = false;
  isLoadingRules = false;
  isSubmitting = false;
  hasSearched = false;
  showUnlockDialog = false;
  locationError = '';

  targetFleetId = 0;
  targetFleetName = '';
  publicRules: PublicFleetRule[] = [];
  acceptedRuleIds = new Set<number>();
  rulesError = '';

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);
  private profileService = inject(ProfileService);
  private profileLocation = inject(ProfileLocationService);
  private cryptoSession = inject(CryptoSessionService);

  constructor() {
    const cached = this.profileLocation.current;
    this.form = this.fb.group({
      mode: ['find' as JoinMode, Validators.required],
      joinCode: [''],
      scope: ['Online' as FleetScope, Validators.required],
      countryCode: [cached?.countryCode ?? null],
      zipCode: [cached?.zipCode ?? '']
    });

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.onBack()
    };
  }

  ngOnInit() {
    this.updatePrimaryButton();
    void this.prefetchLocation();

    this.form.get('mode')?.valueChanges.subscribe(() => {
      this.resetSearch();
      this.updatePrimaryButton();
      this.refreshSearchIfNeeded();
    });

    this.form.get('scope')?.valueChanges.subscribe(() => {
      this.resetSearch();
      this.refreshSearchIfNeeded();
    });

    this.form.valueChanges.pipe(debounceTime(400)).subscribe(() => {
      if (this.isFindMode && this.canSearch()) {
        void this.runSearch(1);
      }
      this.updatePrimaryButton();
    });

    this.refreshSearchIfNeeded();
  }

  get isFindMode(): boolean {
    return this.form.get('mode')?.value === 'find';
  }

  get isLocal(): boolean {
    return this.form.get('scope')?.value === 'Local';
  }

  isInvalid(controlName: string): boolean {
    return isControlInvalidForA11y(this.form.get(controlName));
  }

  get allRulesAccepted(): boolean {
    return this.publicRules.every(rule => this.acceptedRuleIds.has(rule.id));
  }

  isRuleAccepted(ruleId: number): boolean {
    return this.acceptedRuleIds.has(ruleId);
  }

  toggleRuleAcceptance(ruleId: number, accepted: boolean) {
    if (accepted) {
      this.acceptedRuleIds.add(ruleId);
    } else {
      this.acceptedRuleIds.delete(ruleId);
    }
    this.updatePrimaryButton();
  }

  onJoinCodeInput(event: Event) {
    const input = event.target as HTMLInputElement;
    const normalized = input.value.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, JOIN_CODE_LENGTH);
    if (input.value !== normalized) {
      this.form.patchValue({ joinCode: normalized }, { emitEvent: true });
    }
  }

  onCountryCodeChange(code: string | null) {
    this.form.patchValue({ countryCode: code });
    this.locationError = '';
  }

  onUnlockCompleted() {
    this.showUnlockDialog = false;
    void this.prefetchLocation().then(() => this.refreshSearchIfNeeded());
  }

  private onBack() {
    if (this.joinStep === 'rules') {
      this.joinStep = 'select';
      this.publicRules = [];
      this.acceptedRuleIds.clear();
      this.rulesError = '';
      this.updatePrimaryButton();
      return;
    }
    this.navigation.back(['/app/fleet']);
  }

  private updatePrimaryButton() {
    if (this.joinStep === 'rules') {
      const disabled = this.isSubmitting || this.isLoadingRules || !this.allRulesAccepted;
      this.primaryButton = {
        label: 'Request to join',
        type: 'primary',
        disabled,
        onClick: () => this.onSubmitJoinRequest()
      };
      return;
    }

    const disabled = this.isLoadingRules || (this.isFindMode
      ? this.selectedFleet === null
      : this.normalizeJoinCode(this.form.get('joinCode')?.value).length !== JOIN_CODE_LENGTH);

    this.primaryButton = {
      label: 'Select',
      type: 'primary',
      disabled,
      onClick: () => this.onContinueToRules()
    };
  }

  private normalizeJoinCode(value: unknown): string {
    return String(value ?? '').toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, JOIN_CODE_LENGTH);
  }

  canSearch(): boolean {
    if (!this.isFindMode) {
      return false;
    }
    if (!this.isLocal) {
      return true;
    }
    const country = this.form.get('countryCode')?.value;
    const zip = normalizePostalCode(this.form.get('zipCode')?.value);
    return !!country && !!zip;
  }

  private refreshSearchIfNeeded() {
    if (this.canSearch()) {
      void this.runSearch(1);
    }
  }

  private async prefetchLocation(): Promise<void> {
    try {
      const profile = await firstValueFrom(this.profileService.getProfile());
      if (profile.encryptedLocation && this.cryptoSession.isUnlocked()) {
        const location = await this.profileLocation.decrypt(profile.encryptedLocation);
        if (location) {
          this.form.patchValue({
            countryCode: location.countryCode,
            zipCode: location.zipCode
          }, { emitEvent: false });
        }
      }
    } catch {
      // Prefill is best-effort.
    }
  }

  private async ensureLocationSaved(): Promise<{ countryCode: string; zipCode: string } | null> {
    const countryCode = String(this.form.get('countryCode')?.value ?? '').trim().toUpperCase();
    const zipCode = normalizePostalCode(this.form.get('zipCode')?.value);
    if (!countryCode || !zipCode || !isValidPostalCode(zipCode)) {
      this.locationError = 'Enter a country and postal code for Local fleet search.';
      return null;
    }

    if (!this.cryptoSession.isUnlocked()) {
      this.showUnlockDialog = true;
      this.locationError = 'Unlock encryption to save your location to your profile.';
      return null;
    }

    try {
      const encryptedLocation = await this.profileLocation.encrypt(countryCode, zipCode);
      if (!encryptedLocation) {
        this.locationError = 'Enter a country and postal code for Local fleet search.';
        return null;
      }
      const result = await firstValueFrom(this.profileService.updateLocation({ encryptedLocation }));
      if (!result.success) {
        this.locationError = result.message || 'Could not save location.';
        return null;
      }
      this.profileLocation.setPlaintext({ countryCode, zipCode });
      this.locationError = '';
      return { countryCode, zipCode };
    } catch (error) {
      this.locationError = error instanceof Error ? error.message : 'Could not save location.';
      return null;
    }
  }

  async runSearch(page: number) {
    if (!this.canSearch() || this.isSearching) {
      return;
    }

    this.isSearching = true;
    this.currentPage = page;
    const scope = this.form.get('scope')?.value as FleetScope;

    let countryCode: string | null = null;
    let zipCode: string | null = null;
    if (scope === 'Local') {
      const saved = await this.ensureLocationSaved();
      if (!saved) {
        this.isSearching = false;
        this.hasSearched = true;
        this.searchResults = [];
        this.searchMessage = this.locationError || 'Location required';
        this.totalCount = 0;
        this.totalPages = 0;
        this.updatePrimaryButton();
        return;
      }
      countryCode = saved.countryCode;
      zipCode = saved.zipCode;
    }

    this.fleetService.search({
      scope,
      page,
      pageSize: 10,
      countryCode,
      zipCode
    }).subscribe({
      next: (result) => {
        this.hasSearched = true;
        this.searchResults = result.items;
        this.totalPages = result.totalPages;
        this.totalCount = result.totalCount;
        this.searchMessage = result.message;
        this.selectedFleet = null;
        this.isSearching = false;
        this.updatePrimaryButton();
      },
      error: (error) => {
        this.toastService.error(error.error?.message || 'Search failed');
        this.isSearching = false;
      }
    });
  }

  selectFleet(fleet: Fleet) {
    this.selectedFleet = fleet;
    this.updatePrimaryButton();
  }

  goToPage(page: number) {
    if (page < 1 || page > this.totalPages || page === this.currentPage) {
      return;
    }
    void this.runSearch(page);
  }

  resetSearch() {
    this.searchResults = [];
    this.selectedFleet = null;
    this.currentPage = 1;
    this.totalPages = 0;
    this.totalCount = 0;
    this.searchMessage = '';
    this.hasSearched = false;
    this.locationError = '';
  }

  onContinueToRules() {
    if (this.primaryButton.disabled || this.isLoadingRules) {
      return;
    }

    this.isLoadingRules = true;
    this.rulesError = '';
    this.updatePrimaryButton();

    const request = this.isFindMode
      ? this.fleetService.getPublicRules(this.selectedFleet!.id)
      : this.fleetService.getPublicRulesByJoinCode(this.normalizeJoinCode(this.form.get('joinCode')?.value));

    request.subscribe({
      next: (result) => {
        this.isLoadingRules = false;
        if (!result.success) {
          this.toastService.error(result.message);
          this.updatePrimaryButton();
          return;
        }

        this.targetFleetId = result.fleetId;
        this.targetFleetName = result.fleetName;
        this.publicRules = result.items;
        this.acceptedRuleIds.clear();
        this.joinStep = 'rules';
        this.rulesError = '';
        this.updatePrimaryButton();
      },
      error: (error) => {
        this.isLoadingRules = false;
        this.toastService.error(this.extractErrorMessage(error));
        this.updatePrimaryButton();
      }
    });
  }

  onSubmitJoinRequest() {
    if (this.primaryButton.disabled || this.isSubmitting) {
      return;
    }

    this.isSubmitting = true;
    this.updatePrimaryButton();

    const payload = this.isFindMode
      ? {
          fleetId: this.targetFleetId,
          acceptedRuleIds: this.publicRules.map(rule => rule.id)
        }
      : {
          joinCode: this.normalizeJoinCode(this.form.get('joinCode')?.value),
          acceptedRuleIds: this.publicRules.map(rule => rule.id)
        };

    this.fleetService.submitJoinRequest(payload).subscribe({
      next: (result) => {
        if (result.success) {
          this.toastService.success(result.message || 'Join request submitted');
          this.router.navigate(['/app/fleet/join-requests']);
          return;
        }
        this.toastService.error(result.message);
        this.isSubmitting = false;
        this.updatePrimaryButton();
      },
      error: (error) => {
        this.toastService.error(this.extractErrorMessage(error));
        this.isSubmitting = false;
        this.updatePrimaryButton();
      }
    });
  }

  private extractErrorMessage(error: { error?: { message?: string; errors?: Record<string, string[]> } }): string {
    const validationErrors = error.error?.errors;
    if (validationErrors) {
      const firstError = Object.values(validationErrors).flat()[0];
      if (firstError) {
        return firstError;
      }
    }

    return error.error?.message || 'Request failed';
  }
}
